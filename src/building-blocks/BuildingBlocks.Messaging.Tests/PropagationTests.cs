using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace BuildingBlocks.Messaging.Tests;

[Collection(SequentialMessaging.Name)]
public sealed class PropagationTests(MessagingFixture infra)
{
    [Fact]
    public async Task TenantUserAndCorrelationFlowFromHttpThroughTwoHops()
    {
        await using var scenario = await HttpScenario.StartAsync(infra);

        var response = await scenario.PostWidgetAsync(TestPermissions.AllowedUser, "acme", "flow");
        response.EnsureSuccessStatusCode();

        await Waiting.UntilAsync(() => scenario.Archive.Recorder.Count(nameof(WidgetAudited)) == 1, TimeSpan.FromSeconds(60), "the second hop is handled");
        var first = scenario.Auditor.Recorder.All.Single(r => r.Message == nameof(WidgetCreated));
        var second = scenario.Archive.Recorder.All.Single(r => r.Message == nameof(WidgetAudited));

        Assert.Equal("acme", first.TenantId);
        Assert.Equal("acme", second.TenantId);
        Assert.Equal(TestPermissions.AllowedUser, first.InitiatedBy);
        Assert.Equal(TestPermissions.AllowedUser, second.InitiatedBy);
        Assert.False(string.IsNullOrEmpty(first.CorrelationId));
        Assert.Equal(first.CorrelationId, second.CorrelationId);
        Assert.Equal(2, first.VisibleWidgets);
        Assert.Equal(1, second.VisibleWidgets);
    }

    [Fact]
    public async Task MessageWithoutTenantFailsClosed()
    {
        await using var scenario = await HttpScenario.StartAsync(infra);

        await scenario.Api.App.Services.GetRequiredService<IMessageBus>().PublishAsync(new WidgetCreated(Guid.NewGuid(), "platform"));

        await Waiting.UntilAsync(() => scenario.Auditor.Recorder.Count(nameof(WidgetCreated)) == 1, TimeSpan.FromSeconds(60), "the untenanted event is handled");
        var received = scenario.Auditor.Recorder.All.Single(r => r.Message == nameof(WidgetCreated));
        Assert.Null(received.TenantId);
        Assert.Null(received.InitiatedBy);
        Assert.Equal(0, received.VisibleWidgets);
    }
}
