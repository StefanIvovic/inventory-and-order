using Microsoft.Extensions.DependencyInjection;
using Wolverine;
using Wolverine.RabbitMQ;

namespace BuildingBlocks.Messaging;

public static class PermissionInvalidationExtensions
{
    public static string ExchangeFor(string serviceName) => $"{serviceName}.permission-assignments";

    public static WolverineOptions UseBuildingBlocksPermissionInvalidation(this WolverineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.ServiceName))
        {
            throw new InvalidOperationException("Call UseBuildingBlocksMessaging before UseBuildingBlocksPermissionInvalidation.");
        }

        var exchange = ExchangeFor(options.ServiceName);
        var instanceQueue = $"{exchange}.{Guid.NewGuid():N}";

        options.ConfigureRabbitMq().DeclareExchange(exchange, e =>
        {
            e.ExchangeType = ExchangeType.Fanout;
            e.BindQueue(instanceQueue);
        });
        options.PublishMessage<PermissionAssignmentsChanged>().ToRabbitExchange(exchange);
        options.ListenToRabbitQueue(instanceQueue, queue =>
        {
            queue.IsDurable = false;
            queue.IsExclusive = true;
            queue.AutoDelete = true;
        }).BufferedInMemory();
        options.Discovery.IncludeType(typeof(PermissionAssignmentsChangedHandler));
        options.Services.AddSingleton(new PermissionInvalidationEndpoint(exchange, instanceQueue));

        return options;
    }
}

public sealed record PermissionInvalidationEndpoint(string Exchange, string InstanceQueue);
