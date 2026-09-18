using System.Collections.Concurrent;
using OpenCodeSharp.Acp.Protocol;
using OpenCodeSharp.Acp.Rpc;
using OpenCodeSharp.Acp.Transport;

namespace OpenCodeSharp.Acp;

public sealed class OpenCodeAcpClient : IAsyncDisposable
{
    private const int SupportedProtocolVersion = 1;

    private readonly JsonRpcConnection rpc_;
    private readonly ConcurrentDictionary<string, OpenCodeSession> sessions_ = new();
    private bool started_;

    public OpenCodeAcpClient(OpenCodeAcpOptions? options = null)
    {
        var actualOptions = options ?? new OpenCodeAcpOptions();
        var transport = new StdioAcpTransport(actualOptions);
        rpc_ = new JsonRpcConnection(transport, actualOptions.PermissionHandler);
        rpc_.SessionUpdated += OnSessionUpdated;
        rpc_.ErrorReceived += (_, error) => ErrorReceived?.Invoke(error);
    }

    // Library/OpenCode stderr errors are exposed as clean text only.
    public event Action<string>? ErrorReceived;

    public InitializeResult? Initialization { get; private set; }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (started_)
            throw new InvalidOperationException("OpenCode ACP client has already been started.");

        await rpc_.StartAsync(cancellationToken).ConfigureAwait(false);

        Initialization = await rpc_.RequestAsync<InitializeResult>(
            "initialize",
            new InitializeParams
            {
                ProtocolVersion = SupportedProtocolVersion,
                ClientCapabilities = new ClientCapabilities
                {
                    Fs = new FileSystemCapabilities(),
                    Terminal = false
                }
            },
            cancellationToken).ConfigureAwait(false);

        if (Initialization.ProtocolVersion != SupportedProtocolVersion)
        {
            var message = $"Unsupported ACP version: {Initialization.ProtocolVersion}";
            ErrorReceived?.Invoke(message);
            throw new InvalidOperationException(message);
        }

        started_ = true;
    }

    public async Task<OpenCodeSession> CreateSessionAsync(string workingDirectory, CancellationToken cancellationToken = default)
    {
        EnsureStarted();
        var fullPath = Path.GetFullPath(workingDirectory);

        if (!Directory.Exists(fullPath))
        {
            ErrorReceived?.Invoke($"Working directory does not exist: {fullPath}");
            throw new DirectoryNotFoundException(fullPath);
        }

        var result = await rpc_.RequestAsync<SessionNewResult>("session/new", new SessionNewParams { Cwd = fullPath, McpServers = [] }, cancellationToken).ConfigureAwait(false);

        var session = new OpenCodeSession(this, result.SessionId, fullPath);
        sessions_[result.SessionId] = session;
        return session;
    }

    internal Task<PromptResult> PromptAsync(string sessionId, string prompt, CancellationToken cancellationToken)
    {
        EnsureStarted();
        return rpc_.RequestAsync<PromptResult>("session/prompt", new SessionPromptParams { SessionId = sessionId, Prompt = [ContentBlock.FromText(prompt)] }, cancellationToken);
    }

    internal Task CancelSessionAsync(string sessionId, CancellationToken cancellationToken)
    {
        EnsureStarted();
        return rpc_.NotifyAsync("session/cancel", new SessionCancelParams { SessionId = sessionId }, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        started_ = false;
        sessions_.Clear();
        await rpc_.DisposeAsync().ConfigureAwait(false);
    }

    private void OnSessionUpdated(object? sender, AcpSessionUpdate update)
    {
        if (sessions_.TryGetValue(update.SessionId, out var session))
            session.HandleUpdate(update);
    }

    private void EnsureStarted()
    {
        if (!started_)
            throw new InvalidOperationException("Call StartAsync() first.");
    }
}
