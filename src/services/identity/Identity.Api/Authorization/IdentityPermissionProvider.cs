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

        if (await db.Users.AnyAsync(u => u.Id == user.UserId && u.DisabledAt != null, cancellationToken))
        {
            return None;
        }

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
