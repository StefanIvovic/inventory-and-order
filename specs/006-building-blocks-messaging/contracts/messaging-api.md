# Contract: Messaging API

```csharp
namespace BuildingBlocks.Core.Handlers;
public interface IEventContext
{
    ITenantContext Tenant { get; }
    Guid? InitiatedByUserId { get; }
    string? CorrelationId { get; }
    TimeProvider Clock { get; }
}
public abstract class MessageHandler<TDbContext>(IEventContext context, TDbContext db) where TDbContext : class
{
    protected IEventContext Context { get; }
    protected TDbContext Db { get; }
    protected ITenantContext Tenant { get; }
    protected Guid? InitiatedByUserId { get; }
    protected string? CorrelationId { get; }
    protected TimeProvider Clock { get; }
}

namespace BuildingBlocks.Messaging;
public sealed record MessagingSettings(string ServiceName, string PostgresConnectionString, Uri RabbitMqUri);
public static class MessageHeaders { public const string UserId = "user-id"; }
public static class MessagingExtensions
{
    public static WolverineOptions UseBuildingBlocksMessaging(this WolverineOptions options, MessagingSettings settings);
}
public static class RoutingExtensions
{
    public static WolverineOptions PublishEvent<T>(this WolverineOptions options);
    public static WolverineOptions SubscribeToEvent<T>(this WolverineOptions options);
    public static WolverineOptions ListenToServiceQueue(this WolverineOptions options);
}
```

## Service usage (SC-005)

```csharp
builder.Services.AddDbContextWithWolverineIntegration<OrderDbContext>(x => x.UseNpgsql(connectionString));
builder.Host.UseWolverine(opts =>
{
    opts.UseBuildingBlocksMessaging(new MessagingSettings("order", connectionString, rabbitUri));
    opts.PublishEvent<OrderPlaced>();
    opts.SubscribeToEvent<StockReserved>();
    opts.ListenToServiceQueue();
});

[MessageIdentity("order.order-placed.v1")]
public sealed record OrderPlaced(Guid OrderId);

public sealed class StockReservedHandler(IEventContext context, OrderDbContext db)
    : MessageHandler<OrderDbContext>(context, db)
{
    public Task Handle(StockReserved message, CancellationToken ct) => Task.CompletedTask;
}
```
