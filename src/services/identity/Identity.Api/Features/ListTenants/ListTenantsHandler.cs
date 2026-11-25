using BuildingBlocks.Core.Handlers;
using Identity.Api.Authorization;
using Identity.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Identity.Api.Features.ListTenants;

public sealed class ListTenantsHandler(IRequestContext context, IdentityDbContext db) : CommandHandler<IdentityDbContext>(context, db)
{
    public async Task<TenantList> Handle(ListTenants query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        await Permissions.EnsureAsync(IdentityPermissions.TenantsView, cancellationToken);

        var items = await Db.Tenants
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new TenantItem(t.Id, t.Slug, t.Name, t.CreatedAt))
            .ToListAsync(cancellationToken);

        return new TenantList(items, new TenantListActions(await Permissions.HasAsync(IdentityPermissions.TenantsManage, cancellationToken)));
    }
}
