using BuildingBlocks.Core.Tenancy;

namespace BuildingBlocks.Core.Handlers;

public interface IEventContext
{
    ITenantContext Tenant { get; }

    Guid? InitiatedByUserId { get; }

    string? CorrelationId { get; }

    TimeProvider Clock { get; }
}
