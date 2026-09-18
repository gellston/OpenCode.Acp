using OpenCode.Acp.Protocol;

namespace OpenCode.Acp.Permissions;

public interface IAcpPermissionHandler
{
    Task<object> HandleAsync(PermissionRequestParams request, CancellationToken cancellationToken);
}
