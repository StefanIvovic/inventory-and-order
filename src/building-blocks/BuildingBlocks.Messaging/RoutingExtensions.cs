using Wolverine;
using Wolverine.RabbitMQ;
using Wolverine.Util;

namespace BuildingBlocks.Messaging;

public static class RoutingExtensions
{
    public static string IdentityOf<T>() => typeof(T).ToMessageTypeName();

    public static WolverineOptions PublishEvent<T>(this WolverineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var identity = IdentityOf<T>();
        options.ConfigureRabbitMq().DeclareExchange(identity, exchange => exchange.ExchangeType = ExchangeType.Fanout);
        options.PublishMessage<T>().ToRabbitExchange(identity);
        return options;
    }

    public static WolverineOptions SubscribeToEvent<T>(this WolverineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var queue = ServiceQueueOf(options);
        var identity = IdentityOf<T>();
        options.ConfigureRabbitMq().DeclareExchange(identity, exchange =>
        {
            exchange.ExchangeType = ExchangeType.Fanout;
            exchange.BindQueue(queue);
        });
        return options;
    }

    public static WolverineOptions ListenToServiceQueue(this WolverineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.ListenToRabbitQueue(ServiceQueueOf(options)).UseDurableInbox();
        return options;
    }

    private static string ServiceQueueOf(WolverineOptions options) =>
        string.IsNullOrWhiteSpace(options.ServiceName)
            ? throw new InvalidOperationException("Call UseBuildingBlocksMessaging before configuring routing.")
            : options.ServiceName;
}
