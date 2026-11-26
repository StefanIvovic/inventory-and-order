using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using BuildingBlocks.Core.Identity;
using BuildingBlocks.Testing;
using Identity.Api.Users;

namespace Identity.Tests.Lookups;

public sealed class LookupValuesEndpointTests(ContainersFixture containers)
{
    private static readonly Guid UserId = Guid.Parse("b2000000-0000-4000-8000-000000000001");

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public static TheoryData<string> Callers => ["staff", "customer", "platform"];

    [Theory]
    [MemberData(nameof(Callers))]
    public async Task EverySignedInUserReadsBothLists(string caller)
    {
        await using var app = await StartedAsync();
        using var client = app.CreateClient();
        var token = caller switch
        {
            "staff" => IdentityApp.StaffToken(UserId, "acme"),
            "customer" => TestTokens.For(UserId, "acme", UserType.Customer, IdentityApp.Audience),
            _ => TestTokens.Platform(UserId, IdentityApp.Audience),
        };

        var userTypes = await PageAsync(await client.GetAsync("/lookups/user-type/values", token));
        var statuses = await PageAsync(await client.GetAsync("/lookups/staff-status/values", token));

        Assert.Equal(["customer|Customer", "platform|Platform", "staff|Staff"], Items(userTypes));
        Assert.Equal(UserTypes.StaffValue.Id.ToString(), userTypes.GetProperty("items")[2].GetProperty("id").GetString());
        Assert.Equal(JsonValueKind.Null, userTypes.GetProperty("nextCursor").ValueKind);
        Assert.Equal(["active|Active", "disabled|Disabled"], Items(statuses));
    }

    [Fact]
    public async Task AnonymousCallerIs401()
    {
        await using var app = await StartedAsync();
        using var client = app.CreateClient();

        var response = await client.GetAsync("/lookups/user-type/values", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UnknownListIs404()
    {
        await using var app = await StartedAsync();
        using var client = app.CreateClient();

        var response = await client.GetAsync("/lookups/nope/values", IdentityApp.StaffToken(UserId, "acme"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("lookup-type-not-found", (await JsonAsync(response)).GetProperty("code").GetString());
    }

    [Theory]
    [InlineData("?q=TOM", new[] { "customer|Customer" })]
    [InlineData("?codes=staff&codes=platform", new[] { "platform|Platform", "staff|Staff" })]
    [InlineData("?q=a&codes=staff&codes=customer", new[] { "staff|Staff" })]
    [InlineData("?q=zzz", new string[0])]
    public async Task FiltersNarrowTheValues(string query, string[] expected)
    {
        await using var app = await StartedAsync();
        using var client = app.CreateClient();

        var page = await PageAsync(await client.GetAsync("/lookups/user-type/values" + query, IdentityApp.StaffToken(UserId, "acme")));

        Assert.Equal(expected, Items(page));
    }

    [Fact]
    public async Task IdsSelectThoseValues()
    {
        await using var app = await StartedAsync();
        using var client = app.CreateClient();

        var page = await PageAsync(await client.GetAsync(
            $"/lookups/user-type/values?ids={UserTypes.PlatformValue.Id}&ids={Guid.NewGuid()}", IdentityApp.StaffToken(UserId, "acme")));

        Assert.Equal(["platform|Platform"], Items(page));
    }

    [Fact]
    public async Task PagesFollowTheCursor()
    {
        await using var app = await StartedAsync();
        using var client = app.CreateClient();
        var token = IdentityApp.StaffToken(UserId, "acme");

        var first = await PageAsync(await client.GetAsync("/lookups/user-type/values?size=2", token));
        var cursor = first.GetProperty("nextCursor").GetString();
        var second = await PageAsync(await client.GetAsync($"/lookups/user-type/values?size=2&after={cursor}", token));

        Assert.Equal(["customer|Customer", "platform|Platform"], Items(first));
        Assert.Equal(["staff|Staff"], Items(second));
        Assert.Equal(JsonValueKind.Null, second.GetProperty("nextCursor").ValueKind);
    }

    [Theory]
    [InlineData("?size=0", "size")]
    [InlineData("?size=201", "size")]
    [InlineData("?after=broken!", "after")]
    public async Task InvalidFiltersAre400WithTheField(string query, string field)
    {
        await using var app = await StartedAsync();
        using var client = app.CreateClient();

        var response = await client.GetAsync("/lookups/user-type/values" + query, IdentityApp.StaffToken(UserId, "acme"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await JsonAsync(response);
        Assert.Equal("validation-failed", problem.GetProperty("code").GetString());
        Assert.True(problem.GetProperty("errors").TryGetProperty(field, out _), problem.ToString());
    }

    [Fact]
    public async Task RepeatedRequestWithTheTagIsNotModified()
    {
        await using var app = await StartedAsync();
        using var client = app.CreateClient();
        var token = IdentityApp.StaffToken(UserId, "acme");
        var first = await client.GetAsync("/lookups/staff-status/values", token);
        Assert.True(first.Headers.CacheControl?.Private);
        Assert.Equal(TimeSpan.FromSeconds(300), first.Headers.CacheControl?.MaxAge);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/lookups/staff-status/values");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.IfNoneMatch.Add(first.Headers.ETag!);
        var second = await client.SendAsync(request, Cancellation);

        Assert.Equal(HttpStatusCode.NotModified, second.StatusCode);
        Assert.Empty(await second.Content.ReadAsByteArrayAsync(Cancellation));
    }

    private async Task<IdentityApp> StartedAsync()
    {
        var app = await IdentityApp.StartAsync(containers);
        await app.WaitUntilMigratedAsync();
        return app;
    }

    private static async Task<JsonElement> PageAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await JsonAsync(response);
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(Cancellation)).RootElement.Clone();

    private static string[] Items(JsonElement page) =>
        [.. page.GetProperty("items").EnumerateArray().Select(i => $"{i.GetProperty("code").GetString()}|{i.GetProperty("name").GetString()}")];
}
