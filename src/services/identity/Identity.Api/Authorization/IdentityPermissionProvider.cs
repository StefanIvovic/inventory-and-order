using BuildingBlocks.Core.Identity;
using BuildingBlocks.Core.Permissions;
using Identity.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Identity.Api.Authorization;

public sealed class IdentityPermissionProvider(IdentityDbContext db) : IPermissionProvider
{
    public async Task<IReadOnlySet<string>> GetPermissionsAsync(ICurrentUser user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        var roles = user.TenantId is { } tenantId
            ? await db.RoleAssignments
                .Where(a => a.TenantId == tenantId && a.UserId == user.UserId)
                .Select(a => a.Role)
                .ToListAsync(cancellationToken)
            : await db.PlatformRoleAssignments
                .Where(a => a.UserId == user.UserId)
                .Select(a => a.Role)
                .ToListAsync(cancellationToken);

        var allowed = user.TenantId is null ? IdentityRoles.Platform : IdentityRoles.Tenant;
        return roles.Where(allowed.ContainsKey).SelectMany(IdentityRoles.PermissionsOf).ToHashSet(StringComparer.Ordinal);
    }
}
