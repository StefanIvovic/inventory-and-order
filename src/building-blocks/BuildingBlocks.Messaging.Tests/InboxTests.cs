using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using Wolverine;

namespace BuildingBlocks.Messaging.Tests;

[Collection(SequentialMessaging.Name)]
public sealed class InboxTests(MessagingFixture infra)
{
    [Fact]
    public async Task SameMessageIdDeliveredTwiceIsHandledOnce()
    {
        var (subscriber, publisher, raw) = await CaptureWolverineMessageAsync();
        await using (subscriber)
        await using (publisher)
        {
            await Waiting.UntilAsync(() => subscriber.Recorder.Count(nameof(WidgetCreated)) == 1, TimeSpan.FromSeconds(60), "the original delivery is handled");

            await RepublishAsync(raw, subscriber.ServiceName, raw.BasicProperties.MessageId!);
            await RepublishAsync(raw, subscriber.ServiceName, raw.BasicProperties.MessageId!);
            await Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            Assert.Equal(1, subscriber.Recorder.Count(nameof(WidgetCreated)));
        }
    }

    [Fact]
    public async Task DifferentMessageIdsWithSameBodyAreBothHandled()
    {
        var (subscriber, publisher, raw) = await CaptureWolverineMessageAsync();
        await using (subscriber)
        await using (publisher)
        {
            await Waiting.UntilAsync(() => subscriber.Recorder.Count(nameof(WidgetCreated)) == 1, TimeSpan.FromSeconds(60), "the original delivery is handled");

            await RepublishAsync(raw, subscriber.ServiceName, Guid.NewGuid().ToString());

            await Waiting.UntilAsync(() => subscriber.Recorder.Count(nameof(WidgetCreated)) == 2, TimeSpan.FromSeconds(60), "a message with a new id is handled as a new message");
        }
    }

    private async Task<(TestServiceHost Subscriber, TestServiceHost Publisher, BasicGetResult Raw)> CaptureWolverineMessageAsync()
    {
        var cancellation = TestContext.Current.CancellationToken;
        var rabbit = new Uri(infra.RabbitMq.ConnectionString);
        var subscriber = await TestServiceHost.StartAsync(TestServiceHost.UniqueName("sub"), await infra.Postgres.CreateDatabaseAsync(cancellation), rabbit, o => o.SubscribeToEvent<WidgetCreated>().ListenToServiceQueue());
        var publisher = await TestServiceHost.StartAsync(TestServiceHost.UniqueName("pub"), await infra.Postgres.CreateDatabaseAsync(cancellation), rabbit, o => o.PublishEvent<WidgetCreated>());

        var capture = $"capture-{Guid.NewGuid():N}";
        await using var connection = await new ConnectionFactory { Uri = rabbit }.CreateConnectionAsync(cancellation);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellation);
        await channel.QueueDeclareAsync(capture, durable: false, exclusive: true, autoDelete: true, cancellationToken: cancellation);
        await channel.QueueBindAsync(capture, RoutingExtensions.IdentityOf<WidgetCreated>(), string.Empty, cancellationToken: cancellation);

        await publisher.App.Services.GetRequiredService<IMessageBus>().PublishAsync(new WidgetCreated(Guid.NewGuid(), "captured"), new DeliveryOptions { TenantId = "acme" });

        BasicGetResult? raw = null;
        await Waiting.UntilAsync(() =>
        {
            raw ??= channel.BasicGetAsync(capture, autoAck: true, cancellation).GetAwaiter().GetResult();
            return raw is not null;
        }, TimeSpan.FromSeconds(60), "the Wolverine-produced message reaches the capture queue");

        return (subscriber, publisher, raw!);
    }

    private async Task RepublishAsync(BasicGetResult raw, string queue, string messageId)
    {
        var cancellation = TestContext.Current.CancellationToken;
        await using var connection = await new ConnectionFactory { Uri = new Uri(infra.RabbitMq.ConnectionString) }.CreateConnectionAsync(cancellation);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellation);
        var properties = new BasicProperties
        {
            Headers = raw.BasicProperties.Headers,
            ContentType = raw.BasicProperties.ContentType,
            MessageId = messageId,
            CorrelationId = raw.BasicProperties.CorrelationId,
            Type = raw.BasicProperties.Type,
            DeliveryMode = DeliveryModes.Persistent,
        };
        await channel.BasicPublishAsync(string.Empty, queue, mandatory: true, properties, raw.Body, cancellation);
    }
}
