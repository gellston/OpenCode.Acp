using OpenCodeSharp.Acp.Protocol;

namespace OpenCodeSharp.Acp.Permissions;

public interface IAcpPermissionHandler
{
    Task<object> HandleAsync(PermissionRequestParams request, CancellationToken cancellationToken);
}
