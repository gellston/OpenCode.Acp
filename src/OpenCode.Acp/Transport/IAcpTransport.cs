namespace OpenCode.Acp.Transport;

internal interface IAcpTransport : IAsyncDisposable
{
    event EventHandler<string>? LogReceived;
    Task StartAsync(CancellationToken cancellationToken = default);
    Task SendLineAsync(string message, CancellationToken cancellationToken = default);
    Task<string?> ReadLineAsync(CancellationToken cancellationToken = default);
}
