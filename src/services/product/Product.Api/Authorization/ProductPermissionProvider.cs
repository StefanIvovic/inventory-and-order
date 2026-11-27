using BuildingBlocks.Core.Identity;
using BuildingBlocks.Core.Permissions;
using Microsoft.EntityFrameworkCore;
using Product.Api.Persistence;

namespace Product.Api.Authorization;

public sealed class ProductPermissionProvider(ProductDbContext db) : IPermissionProvider
{
    private static readonly IReadOnlySet<string> None = new HashSet<string>();

    public async Task<IReadOnlySet<string>> GetPermissionsAsync(ICurrentUser user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (user.UserType != UserType.Staff || user.TenantId is not { } tenantId)
        {
            return None;
        }

        var roles = await db.RoleAssignments
            .Where(a => a.TenantId == tenantId && a.UserId == user.UserId)
            .Select(a => a.Role)
            .ToListAsync(cancellationToken);

        return roles.SelectMany(ProductRoles.PermissionsOf).ToHashSet(StringComparer.Ordinal);
    }
}
