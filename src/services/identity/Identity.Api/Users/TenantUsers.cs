using BuildingBlocks.Core.Tenancy;
using Identity.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Identity.Api.Users;

public static class TenantUsers
{
    public static async Task<AppUser> FindAsync(IdentityDbContext db, ITenantContext tenant, Guid userId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(tenant);
        var tenantId = tenant.TenantId;
        return await db.Users.SingleOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId && u.UserType == UserTypes.Staff, cancellationToken)
            ?? throw UserErrors.NotFound();
    }
}
