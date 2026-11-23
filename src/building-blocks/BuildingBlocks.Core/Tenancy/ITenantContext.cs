namespace BuildingBlocks.Core.Tenancy;

public interface ITenantContext
{
    string? TenantId { get; }

    bool HasTenant { get; }
}
