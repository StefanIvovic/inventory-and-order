using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BuildingBlocks.Testing;

namespace Identity.Tests.Tenants;

public sealed class ListTenantsTests(ContainersFixture containers)
{
    [Fact]
    public async Task PlatformAdminSeesEveryTenantNewestFirst()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        var first = await app.CreateTenantAsync("acme");
        var second = await app.CreateTenantAsync("globex");
        using var client = app.CreateClient();

        var response = await client.GetAsync("/tenants", await app.PlatformTokenAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var items = body.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal([second.Id, first.Id], items.Select(i => i.GetProperty("id").GetString()));
        Assert.Equal(["globex", "acme"], items.Select(i => i.GetProperty("slug").GetString()));
        Assert.Equal("globex Ltd", items[0].GetProperty("name").GetString());
        Assert.True(items[0].TryGetProperty("createdAt", out _));
        Assert.True(body.GetProperty("actions").GetProperty("canCreate").GetBoolean());
    }

    [Fact]
    public async Task PlatformUserWithoutThePlatformRoleIsForbidden()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        await app.WaitUntilMigratedAsync();
        using var client = app.CreateClient();

        var response = await client.GetAsync("/tenants", TestTokens.Platform(Guid.NewGuid(), IdentityApp.Audience));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
