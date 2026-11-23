using BuildingBlocks.Core.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace BuildingBlocks.EntityFrameworkCore;

internal sealed class TenantSaveChangesInterceptor(ITenantContext tenantContext) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Apply(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Apply(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Apply(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        foreach (var entry in context.ChangeTracker.Entries<ITenantOwned>())
        {
            var tenant = entry.Property(nameof(ITenantOwned.TenantId));
            var entityName = entry.Metadata.DisplayName();

            switch (entry.State)
            {
                case EntityState.Added:
                    if (!tenantContext.HasTenant)
                    {
                        throw new TenantViolationException($"Cannot insert {entityName} without a tenant in the current context.");
                    }

                    var assigned = tenant.CurrentValue as string;
                    if (string.IsNullOrEmpty(assigned))
                    {
                        tenant.CurrentValue = tenantContext.TenantId;
                    }
                    else if (!string.Equals(assigned, tenantContext.TenantId, StringComparison.Ordinal))
                    {
                        throw new TenantViolationException($"Cannot insert {entityName} for a different tenant than the current context.");
                    }

                    break;

                case EntityState.Modified when tenant.IsModified:
                    throw new TenantViolationException($"The tenant of an existing {entityName} cannot be changed.");
            }
        }
    }
}
