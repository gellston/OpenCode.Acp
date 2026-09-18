using OpenCode.Acp.Permissions;

namespace OpenCode.Acp;

public sealed class OpenCodeAcpOptions
{
    public string ExecutablePath { get; init; } = "opencode";
    public string? ProcessWorkingDirectory { get; init; }
    public bool PrintLogs { get; init; }
    public bool LaunchViaCmdOnWindows { get; init; } = true;
    public bool ForceUtf8OnWindows { get; init; } = true;
    public TimeSpan ShutdownTimeout { get; init; } = TimeSpan.FromSeconds(5);
    public IAcpPermissionHandler PermissionHandler { get; init; } = new DenyAllPermissionHandler();
}
