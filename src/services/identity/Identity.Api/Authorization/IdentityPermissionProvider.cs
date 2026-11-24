using BuildingBlocks.Core.Identity;
using BuildingBlocks.Core.Permissions;
using Identity.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Identity.Api.Authorization;

public sealed class IdentityPermissionProvider(IdentityDbContext db) : IPermissionProvider
{
    private static readonly IReadOnlySet<string> None = new HashSet<string>();

    public async Task<IReadOnlySet<string>> GetPermissionsAsync(ICurrentUser user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (user.TenantId is not { } tenantId)
        {
            return None;
        }

        var roles = await db.RoleAssignments
            .Where(a => a.TenantId == tenantId && a.UserId == user.UserId)
            .Select(a => a.Role)
            .ToListAsync(cancellationToken);

        return roles.SelectMany(IdentityRoles.PermissionsOf).ToHashSet(StringComparer.Ordinal);
    }
}
