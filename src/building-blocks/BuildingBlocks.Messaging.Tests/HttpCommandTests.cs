using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace BuildingBlocks.Messaging.Tests;

[Collection(SequentialMessaging.Name)]
public sealed class HttpCommandTests(MessagingFixture infra)
{
    [Fact]
    public async Task MappedRouteReturnsResponsePersistsAndCascades()
    {
        await using var scenario = await HttpScenario.StartAsync(infra);

        var response = await scenario.PostWidgetAsync(TestPermissions.AllowedUser, "acme", "via-http");
        var body = await response.Content.ReadFromJsonAsync<WidgetCreatedResponse>(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEqual(Guid.Empty, body!.WidgetId);
        Assert.Equal(1, await scenario.Api.CountAllWidgetsAsync());
        await Waiting.UntilAsync(() => scenario.Auditor.Recorder.Count(nameof(WidgetCreated)) == 1, TimeSpan.FromSeconds(60), "the cascaded event is delivered");
    }

    [Fact]
    public async Task DeniedCommandReturns403AndChangesNothing()
    {
        await using var scenario = await HttpScenario.StartAsync(infra);

        var response = await scenario.PostWidgetAsync(TestPermissions.DeniedUser, "acme", "denied");
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("permission-denied", problem.GetProperty("code").GetString());
        Assert.Equal(0, await scenario.Api.CountAllWidgetsAsync());
        await Task.Delay(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        Assert.Equal(0, scenario.Auditor.Recorder.Count(nameof(WidgetCreated)));
    }
}
