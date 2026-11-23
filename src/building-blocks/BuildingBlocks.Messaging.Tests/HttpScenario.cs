using System.Net.Http.Json;

namespace BuildingBlocks.Messaging.Tests;

public sealed class HttpScenario : IAsyncDisposable
{
    private HttpScenario(TestServiceHost api, TestServiceHost auditor, TestServiceHost archive)
    {
        Api = api;
        Auditor = auditor;
        Archive = archive;
    }

    public TestServiceHost Api { get; }

    public TestServiceHost Auditor { get; }

    public TestServiceHost Archive { get; }

    public static async Task<HttpScenario> StartAsync(MessagingFixture infra)
    {
        var cancellation = TestContext.Current.CancellationToken;
        var rabbit = new Uri(infra.RabbitMq.ConnectionString);
        var archive = await TestServiceHost.StartAsync(TestServiceHost.UniqueName("archive"), await infra.Postgres.CreateDatabaseAsync(cancellation), rabbit, o => o.SubscribeToEvent<WidgetAudited>().ListenToServiceQueue());
        var auditor = await TestServiceHost.StartAsync(TestServiceHost.UniqueName("auditor"), await infra.Postgres.CreateDatabaseAsync(cancellation), rabbit, o => o.SubscribeToEvent<WidgetCreated>().ListenToServiceQueue().PublishEvent<WidgetAudited>());
        var api = await TestServiceHost.StartAsync(TestServiceHost.UniqueName("api"), await infra.Postgres.CreateDatabaseAsync(cancellation), rabbit, o => o.PublishEvent<WidgetCreated>());
        auditor.Recorder.CascadeAudit = true;
        await auditor.SeedAsync("acme", "a1", "a2");
        await auditor.SeedAsync("globex", "g1");
        await archive.SeedAsync("acme", "a1");
        await archive.SeedAsync("globex", "g1", "g2");
        return new HttpScenario(api, auditor, archive);
    }

    public async Task<HttpResponseMessage> PostWidgetAsync(Guid user, string tenant, string name)
    {
        using var client = Api.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/widgets") { Content = JsonContent.Create(new CreateWidget(name)) };
        request.Headers.Add(TestAuthHandler.UserHeader, user.ToString());
        request.Headers.Add(TestAuthHandler.TenantHeader, tenant);
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await Api.DisposeAsync();
        await Auditor.DisposeAsync();
        await Archive.DisposeAsync();
    }
}
