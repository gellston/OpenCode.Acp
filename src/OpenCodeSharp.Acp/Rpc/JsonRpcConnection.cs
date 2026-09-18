using System.Collections.Concurrent;
using System.Text.Json;
using OpenCodeSharp.Acp.Permissions;
using OpenCodeSharp.Acp.Protocol;
using OpenCodeSharp.Acp.Transport;

namespace OpenCodeSharp.Acp.Rpc;

internal sealed class JsonRpcConnection : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IAcpTransport transport_;
    private readonly IAcpPermissionHandler permissionHandler_;
    private readonly ConcurrentDictionary<long, TaskCompletionSource<JsonElement>> pending_ = new();
    private long nextId_;
    private CancellationTokenSource? receiveCts_;
    private Task? receiveTask_;

    public JsonRpcConnection(IAcpTransport transport, IAcpPermissionHandler permissionHandler)
    {
        transport_ = transport;
        permissionHandler_ = permissionHandler;
        transport_.LogReceived += (_, line) => ErrorReceived?.Invoke(this, line);
    }

    public event EventHandler<AcpSessionUpdate>? SessionUpdated;
    public event EventHandler<string>? ErrorReceived;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await transport_.StartAsync(cancellationToken).ConfigureAwait(false);
            receiveCts_ = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            receiveTask_ = ReceiveLoopAsync(receiveCts_.Token);
        }
        catch (Exception ex)
        {
            ReportError(ex);
            throw;
        }
    }

    public async Task<T> RequestAsync<T>(string method, object? parameters, CancellationToken cancellationToken = default)
    {
        var id = Interlocked.Increment(ref nextId_);
        var tcs = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        pending_[id] = tcs;

        try
        {
            await SendAsync(new Dictionary<string, object?>
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id,
                ["method"] = method,
                ["params"] = parameters
            }, cancellationToken).ConfigureAwait(false);

            using var registration = cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));
            var result = await tcs.Task.ConfigureAwait(false);

            return result.Deserialize<T>(JsonOptions)
                ?? throw new InvalidOperationException($"Invalid ACP result for {method}.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            ReportError(ex);
            throw;
        }
        finally
        {
            pending_.TryRemove(id, out _);
        }
    }

    public async Task NotifyAsync(string method, object? parameters, CancellationToken cancellationToken = default)
    {
        try
        {
            await SendAsync(new Dictionary<string, object?>
            {
                ["jsonrpc"] = "2.0",
                ["method"] = method,
                ["params"] = parameters
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            ReportError(ex);
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (receiveCts_ is not null)
        {
            receiveCts_.Cancel();
            receiveCts_.Dispose();
        }

        if (receiveTask_ is not null)
        {
            try { await receiveTask_.ConfigureAwait(false); } catch { }
        }

        await transport_.DisposeAsync().ConfigureAwait(false);
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await transport_.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (line is null)
                    break;
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                try
                {
                    using var document = JsonDocument.Parse(line);
                    await HandleMessageAsync(document.RootElement.Clone(), cancellationToken).ConfigureAwait(false);
                }
                catch (JsonException ex)
                {
                    ReportError(new InvalidOperationException($"Failed to parse ACP JSON: {ex.Message}", ex));
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    ReportError(ex);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            ReportError(ex);

            foreach (var item in pending_.Values)
                item.TrySetException(ex);
        }
    }

    private async Task HandleMessageAsync(JsonElement message, CancellationToken cancellationToken)
    {
        var hasMethod = message.TryGetProperty("method", out var methodElement) && methodElement.ValueKind == JsonValueKind.String;
        var hasId = message.TryGetProperty("id", out var idElement);

        if (hasMethod)
        {
            var method = methodElement.GetString()!;

            if (hasId)
                await HandleIncomingRequestAsync(idElement.Clone(), method, message, cancellationToken).ConfigureAwait(false);
            else
                HandleNotification(method, message);

            return;
        }

        if (hasId)
            HandleResponse(idElement, message);
    }

    private void HandleNotification(string method, JsonElement message)
    {
        if (method != "session/update" || !message.TryGetProperty("params", out var parameters))
            return;

        var update = parameters.Deserialize<SessionUpdateParams>(JsonOptions);
        if (update is not null)
            SessionUpdated?.Invoke(this, new AcpSessionUpdate(update.SessionId, update.Update));
    }

    private void HandleResponse(JsonElement idElement, JsonElement message)
    {
        if (!idElement.TryGetInt64(out var id) || !pending_.TryGetValue(id, out var tcs))
            return;

        if (message.TryGetProperty("error", out var error))
        {
            var exception = new InvalidOperationException($"ACP error response: {error}");
            tcs.TrySetException(exception);
            return;
        }

        if (message.TryGetProperty("result", out var result))
            tcs.TrySetResult(result.Clone());
        else
            tcs.TrySetResult(JsonSerializer.SerializeToElement(new { }, JsonOptions));
    }

    private async Task HandleIncomingRequestAsync(JsonElement id, string method, JsonElement message, CancellationToken cancellationToken)
    {
        if (method == "session/request_permission" && message.TryGetProperty("params", out var parameters))
        {
            try
            {
                var request = parameters.Deserialize<PermissionRequestParams>(JsonOptions);
                if (request is not null)
                {
                    var result = await permissionHandler_.HandleAsync(request, cancellationToken).ConfigureAwait(false);
                    await SendAsync(new Dictionary<string, object?>
                    {
                        ["jsonrpc"] = "2.0",
                        ["id"] = id.Clone(),
                        ["result"] = result
                    }, cancellationToken).ConfigureAwait(false);
                    return;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                ReportError(ex);
            }
        }

        await SendAsync(new Dictionary<string, object?>
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id.Clone(),
            ["error"] = new { code = -32601, message = $"Unsupported ACP client method: {method}" }
        }, cancellationToken).ConfigureAwait(false);
    }

    private async Task SendAsync(object message, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(message, JsonOptions);
        await transport_.SendLineAsync(json, cancellationToken).ConfigureAwait(false);
    }

    private void ReportError(Exception exception)
    {
        ErrorReceived?.Invoke(this, exception.Message);
    }
}
