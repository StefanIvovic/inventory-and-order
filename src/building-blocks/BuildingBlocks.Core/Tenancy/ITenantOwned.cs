namespace BuildingBlocks.Core.Tenancy;

public interface ITenantOwned
{
    string TenantId { get; }
}
