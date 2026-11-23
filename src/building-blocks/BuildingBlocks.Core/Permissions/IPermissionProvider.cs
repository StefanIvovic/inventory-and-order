using BuildingBlocks.Core.Identity;

namespace BuildingBlocks.Core.Permissions;

public interface IPermissionProvider
{
    Task<IReadOnlySet<string>> GetPermissionsAsync(ICurrentUser user, CancellationToken cancellationToken);
}
