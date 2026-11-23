using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using Wolverine;

namespace BuildingBlocks.Messaging.Tests;

[Collection(SequentialMessaging.Name)]
public sealed class RoutingTests(MessagingFixture infra)
{
    [Fact]
    public async Task EachSubscribingServiceGetsOneCopyAndOthersNone()
    {
        await using var s1 = await StartAsync("s1", o => o.SubscribeToEvent<WidgetCreated>().ListenToServiceQueue());
        await using var s2 = await StartAsync("s2", o => o.SubscribeToEvent<WidgetCreated>().ListenToServiceQueue());
        await using var s3 = await StartAsync("s3", o => o.SubscribeToEvent<WidgetAudited>().ListenToServiceQueue());
        await using var publisher = await StartAsync("pub", o => o.PublishEvent<WidgetCreated>());

        await publisher.App.Services.GetRequiredService<IMessageBus>()
            .PublishAsync(new WidgetCreated(Guid.NewGuid(), "routed"), new DeliveryOptions { TenantId = "acme" });

        await Waiting.UntilAsync(() => s1.Recorder.Count(nameof(WidgetCreated)) == 1 && s2.Recorder.Count(nameof(WidgetCreated)) == 1, TimeSpan.FromSeconds(60), "both subscribers handle the event");
        await Task.Delay(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);

        Assert.Equal(1, s1.Recorder.Count(nameof(WidgetCreated)));
        Assert.Equal(1, s2.Recorder.Count(nameof(WidgetCreated)));
        Assert.Equal(0, s3.Recorder.Count(nameof(WidgetCreated)));
    }

    [Fact]
    public async Task IdentityIsUsedOnTheWireAndAsExchangeName()
    {
        await using var subscriber = await StartAsync("sub", o => o.SubscribeToEvent<WidgetCreated>().ListenToServiceQueue());
        await using var publisher = await StartAsync("pub", o => o.PublishEvent<WidgetCreated>());

        await publisher.App.Services.GetRequiredService<IMessageBus>()
            .PublishAsync(new WidgetCreated(Guid.NewGuid(), "identity"), new DeliveryOptions { TenantId = "acme" });
        await Waiting.UntilAsync(() => subscriber.Recorder.Count(nameof(WidgetCreated)) == 1, TimeSpan.FromSeconds(60), "the subscriber handles the event");

        Assert.Equal("test.widget-created.v1", subscriber.Recorder.All.Single().MessageType);
        var cancellation = TestContext.Current.CancellationToken;
        await using var connection = await new ConnectionFactory { Uri = new Uri(infra.RabbitMq.ConnectionString) }.CreateConnectionAsync(cancellation);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellation);
        await channel.ExchangeDeclarePassiveAsync("test.widget-created.v1", cancellation);
    }

    [Fact]
    public async Task SubscriberStartedBeforeAnyPublishStillReceives()
    {
        await using var subscriber = await StartAsync("early", o => o.SubscribeToEvent<WidgetAudited>().ListenToServiceQueue());
        await using var publisher = await StartAsync("late", o => o.PublishEvent<WidgetAudited>());

        await publisher.App.Services.GetRequiredService<IMessageBus>()
            .PublishAsync(new WidgetAudited(Guid.NewGuid()), new DeliveryOptions { TenantId = "acme" });

        await Waiting.UntilAsync(() => subscriber.Recorder.Count(nameof(WidgetAudited)) == 1, TimeSpan.FromSeconds(60), "the early subscriber receives the first event");
    }

    private async Task<TestServiceHost> StartAsync(string prefix, Action<WolverineOptions> routing) =>
        await TestServiceHost.StartAsync(
            TestServiceHost.UniqueName(prefix),
            await infra.Postgres.CreateDatabaseAsync(TestContext.Current.CancellationToken),
            new Uri(infra.RabbitMq.ConnectionString),
            routing);
}
