using BuildingBlocks.Core.Tenancy;

namespace BuildingBlocks.Core.Handlers;

public abstract class MessageHandler<TDbContext>
    where TDbContext : class
{
    protected MessageHandler(IEventContext context, TDbContext db)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(db);
        Context = context;
        Db = db;
    }

    protected IEventContext Context { get; }

    protected TDbContext Db { get; }

    protected ITenantContext Tenant => Context.Tenant;

    protected Guid? InitiatedByUserId => Context.InitiatedByUserId;

    protected string? CorrelationId => Context.CorrelationId;

    protected TimeProvider Clock => Context.Clock;
}
