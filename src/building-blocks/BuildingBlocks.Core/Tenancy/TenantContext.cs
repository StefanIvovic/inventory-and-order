namespace BuildingBlocks.Core.Tenancy;

public sealed class TenantContext : ITenantContext
{
    public string? TenantId { get; private set; }

    public bool HasTenant => TenantId is not null;

    public void Set(string tenantId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        if (TenantId is null)
        {
            TenantId = tenantId;
            return;
        }

        if (!string.Equals(TenantId, tenantId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"The tenant context is already set to '{TenantId}' and cannot be changed to '{tenantId}'.");
        }
    }
}
