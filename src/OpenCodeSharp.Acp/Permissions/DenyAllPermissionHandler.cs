using OpenCodeSharp.Acp.Protocol;

namespace OpenCodeSharp.Acp.Permissions;

public sealed class DenyAllPermissionHandler : IAcpPermissionHandler
{
    public Task<object> HandleAsync(PermissionRequestParams request, CancellationToken cancellationToken)
    {
        return Task.FromResult<object>(new { outcome = new { outcome = "cancelled" } });
    }
}
