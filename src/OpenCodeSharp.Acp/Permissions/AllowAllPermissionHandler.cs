using OpenCodeSharp.Acp.Protocol;

namespace OpenCodeSharp.Acp.Permissions;

public sealed class AllowAllPermissionHandler : IAcpPermissionHandler
{
    public Task<object> HandleAsync(PermissionRequestParams request, CancellationToken cancellationToken)
    {
        var option = request.Options.FirstOrDefault(x => string.Equals(x.Kind, "allow_once", StringComparison.OrdinalIgnoreCase))
            ?? request.Options.FirstOrDefault(x => x.Kind.StartsWith("allow", StringComparison.OrdinalIgnoreCase));

        if (option is null)
            return Task.FromResult<object>(new { outcome = new { outcome = "cancelled" } });

        return Task.FromResult<object>(new { outcome = new { outcome = "selected", optionId = option.OptionId } });
    }
}
