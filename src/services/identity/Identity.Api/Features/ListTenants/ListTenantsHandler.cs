using System.Diagnostics.CodeAnalysis;
using BuildingBlocks.Core.Handlers;
using BuildingBlocks.Validation;
using Identity.Api.Authorization;
using Identity.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Identity.Api.Features.ListTenants;

public sealed class ListTenantsHandler(IRequestContext context, IdentityDbContext db, ICommandValidation<ListTenants> validation)
    : CommandHandler<IdentityDbContext>(context, db)
{
    [SuppressMessage("Globalization", "CA1309", Justification = "Translated to a SQL comparison in the column collation.")]
    [SuppressMessage("Globalization", "CA1310", Justification = "Translated to a SQL prefix match.")]
    public async Task<TenantList> Handle(ListTenants query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        await Permissions.EnsureAsync(IdentityPermissions.TenantsView, cancellationToken);
        await validation.EnsureValidAsync(query, cancellationToken);
        var after = TenantCursor.Decode(query.After);

        var tenants = Db.Tenants.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var text = query.Q.Trim();
            var slugPrefix = text.ToLowerInvariant();
            var namePrefix = text.ToUpperInvariant();
            tenants = tenants.Where(t => t.Slug.StartsWith(slugPrefix) || t.NormalizedName.StartsWith(namePrefix));
        }

        var total = query.IncludeTotal ? await tenants.CountAsync(cancellationToken) : (int?)null;

        if (after is not null)
        {
            var slug = after.Slug;
            tenants = tenants.Where(t => string.Compare(t.Slug, slug) > 0);
        }

        var rows = await tenants
            .OrderBy(t => t.Slug)
            .Take(query.Size + 1)
            .Select(t => new TenantItem(t.Id, t.Slug, t.Name, t.CreatedAt))
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > query.Size;
        var page = hasMore ? rows[..query.Size] : rows;
        var next = hasMore ? new TenantCursor(page[^1].Slug).Encode() : null;
        var canCreate = await Permissions.HasAsync(IdentityPermissions.TenantsManage, cancellationToken);

        return new TenantList(page, next, total, new TenantListActions(canCreate));
    }
}
