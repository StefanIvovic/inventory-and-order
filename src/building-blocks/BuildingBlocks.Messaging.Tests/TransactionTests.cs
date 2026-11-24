using BuildingBlocks.Testing;
using Wolverine;
using Microsoft.Extensions.DependencyInjection;
using Wolverine.Tracking;

namespace BuildingBlocks.Messaging.Tests;

[Collection(SequentialMessaging.Name)]
public sealed class TransactionTests(MessagingFixture infra)
{
    [Fact]
    public async Task SuccessfulHandlerCommitsEntityAndDeliversEvent()
    {
        await using var publisher = await StartPublisherAsync();
        await using var subscriber = await StartSubscriberAsync();

        await publisher.App.TrackActivity()
            .IncludeExternalTransports()
            .AlsoTrack(subscriber.App)
            .Timeout(TimeSpan.FromSeconds(60))
            .ExecuteAndWaitAsync((IMessageContext context) => context.InvokeForTenantAsync("acme", new AddWidget("ok", FailAfterAdd: false), TestContext.Current.CancellationToken));

        Assert.Equal(1, await publisher.CountAllWidgetsAsync());
        Assert.Equal(1, subscriber.Recorder.Count(nameof(WidgetCreated)));
    }

    [Fact]
    public async Task FailingHandlerPersistsNothingAndDeliversNothing()
    {
        await using var publisher = await StartPublisherAsync();
        await using var subscriber = await StartSubscriberAsync();

        var session = await publisher.App.TrackActivity()
            .IncludeExternalTransports()
            .AlsoTrack(subscriber.App)
            .DoNotAssertOnExceptionsDetected()
            .Timeout(TimeSpan.FromSeconds(60))
            .ExecuteAndWaitAsync(async Task (IMessageContext context) =>
            {
                await Assert.ThrowsAnyAsync<Exception>(() => context.InvokeForTenantAsync("acme", new AddWidget("boom", FailAfterAdd: true), TestContext.Current.CancellationToken));
            });

        Assert.Equal(0, await publisher.CountAllWidgetsAsync());
        Assert.Empty(session.Sent.MessagesOf<WidgetCreated>());
        Assert.DoesNotContain(publisher.Logs.Entries, e => e.Message.Contains("boom", StringComparison.Ordinal));
        await Task.Delay(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        Assert.Equal(0, subscriber.Recorder.Count(nameof(WidgetCreated)));
    }

    private Task<TestServiceHost> StartPublisherAsync() => StartAsync("pub", infra.RabbitMq, o => o.PublishEvent<WidgetCreated>());

    private Task<TestServiceHost> StartSubscriberAsync() => StartAsync("sub", infra.RabbitMq, o => o.SubscribeToEvent<WidgetCreated>().ListenToServiceQueue());

    private async Task<TestServiceHost> StartAsync(string prefix, RabbitMqFixture rabbit, Action<WolverineOptions> routing) =>
        await TestServiceHost.StartAsync(
            TestServiceHost.UniqueName(prefix),
            await infra.Postgres.CreateDatabaseAsync(TestContext.Current.CancellationToken),
            new Uri(rabbit.ConnectionString),
            routing);
}

[Collection(SequentialMessaging.Name)]
public sealed class BrokerOutageTests(MessagingFixture infra)
{
    [Fact]
    public async Task EventCommittedWhileBrokerIsDownIsDeliveredAfterRecovery()
    {
        await using var ownBroker = new RabbitMqFixture();
        await ownBroker.InitializeAsync();
        var rabbit = new Uri(ownBroker.ConnectionString);
        await using var publisher = await TestServiceHost.StartAsync(
            TestServiceHost.UniqueName("pub"), await infra.Postgres.CreateDatabaseAsync(TestContext.Current.CancellationToken), rabbit, o => o.PublishEvent<WidgetCreated>());
        await using var subscriber = await TestServiceHost.StartAsync(
            TestServiceHost.UniqueName("sub"), await infra.Postgres.CreateDatabaseAsync(TestContext.Current.CancellationToken), rabbit, o => o.SubscribeToEvent<WidgetCreated>().ListenToServiceQueue());

        await Docker("pause", ownBroker.ContainerId);
        try
        {
            await publisher.App.Services.GetRequiredService<IMessageBus>().InvokeForTenantAsync("acme", new AddWidget("while-down", FailAfterAdd: false), TestContext.Current.CancellationToken);

            Assert.Equal(1, await publisher.CountAllWidgetsAsync());
            await Task.Delay(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
            Assert.Equal(0, subscriber.Recorder.Count(nameof(WidgetCreated)));
        }
        finally
        {
            await Docker("unpause", ownBroker.ContainerId);
        }

        await Waiting.UntilAsync(() => subscriber.Recorder.Count(nameof(WidgetCreated)) == 1, TimeSpan.FromSeconds(120), "the stored event is delivered after the broker recovers");
    }

    private static async Task Docker(string verb, string containerId)
    {
        using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("docker", [verb, containerId]) { RedirectStandardOutput = true, RedirectStandardError = true })!;
        await process.WaitForExitAsync();
        Assert.Equal(0, process.ExitCode);
    }
}
