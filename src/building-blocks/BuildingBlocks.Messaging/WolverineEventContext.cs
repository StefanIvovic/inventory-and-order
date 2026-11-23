using BuildingBlocks.Core.Handlers;
using BuildingBlocks.Core.Tenancy;
using Wolverine;

namespace BuildingBlocks.Messaging;

internal sealed class WolverineEventContext(IMessageContext messageContext, ITenantContext tenant, TimeProvider clock) : IEventContext
{
    public ITenantContext Tenant { get; } = tenant;

    public Guid? InitiatedByUserId =>
        messageContext.Envelope?.Headers.TryGetValue(MessageHeaders.UserId, out var value) == true &&
        Guid.TryParse(value, out var userId)
            ? userId
            : null;

    public string? CorrelationId => messageContext.CorrelationId;

    public TimeProvider Clock { get; } = clock;
}
