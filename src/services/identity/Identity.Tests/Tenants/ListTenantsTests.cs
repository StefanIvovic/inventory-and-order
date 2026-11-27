using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BuildingBlocks.Testing;
using Npgsql;

namespace Identity.Tests.Tenants;

public sealed class ListTenantsTests(ContainersFixture containers)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task TenantsComeInIdentifierByteOrderWithTheirDetails()
    {
        await using var platform = await PlatformAsync();
        var created = await platform.App.CreateTenantAsync("globex");
        await TenantSeed.InsertAsync(platform.Database, ("b-c", "B C"), ("abc", "Abc"), ("a1c", "A1c"), ("a-bc", "A bc"));

        var page = await platform.PageAsync(string.Empty);

        Assert.Equal(["a-bc", "a1c", "abc", "b-c", "globex"], Slugs(page));
        var globex = page.GetProperty("items")[4];
        Assert.Equal(created.Id, globex.GetProperty("id").GetString());
        Assert.Equal("globex Ltd", globex.GetProperty("name").GetString());
        Assert.True(globex.TryGetProperty("createdAt", out _));
        Assert.True(page.GetProperty("actions").GetProperty("canCreate").GetBoolean());
        Assert.Equal(JsonValueKind.Null, page.GetProperty("nextCursor").ValueKind);
        Assert.False(page.TryGetProperty("total", out _));
    }

    [Fact]
    public async Task SmallPagesWalkEveryTenantOnce()
    {
        await using var platform = await PlatformAsync();
        await TenantSeed.InsertNumberedAsync(platform.Database, 5);
        var pages = new List<string[]>();
        string? after = null;

        do
        {
            var page = await platform.PageAsync(after is null ? "?size=2" : $"?size=2&after={after}");
            pages.Add(Slugs(page));
            after = page.GetProperty("nextCursor").GetString();
        }
        while (after is not null);

        Assert.Equal([2, 2, 1], pages.Select(p => p.Length));
        Assert.Equal(Enumerable.Range(1, 5).Select(TenantSeed.Slug), pages.SelectMany(p => p));
    }

    [Fact]
    public async Task DefaultPageHolds25()
    {
        await using var platform = await PlatformAsync();
        await TenantSeed.InsertNumberedAsync(platform.Database, 30);

        var first = await platform.PageAsync(string.Empty);
        var second = await platform.PageAsync($"?after={first.GetProperty("nextCursor").GetString()}");

        Assert.Equal(Enumerable.Range(1, 25).Select(TenantSeed.Slug), Slugs(first));
        Assert.Equal(Enumerable.Range(26, 5).Select(TenantSeed.Slug), Slugs(second));
        Assert.Equal(JsonValueKind.Null, second.GetProperty("nextCursor").ValueKind);
    }

    [Fact]
    public async Task TotalIsCountedOnlyWhenAsked()
    {
        await using var platform = await PlatformAsync();
        await TenantSeed.InsertNumberedAsync(platform.Database, 12);

        var counted = await platform.PageAsync("?size=5&includeTotal=true");
        var next = await platform.PageAsync($"?size=5&after={counted.GetProperty("nextCursor").GetString()}");

        Assert.Equal(12, counted.GetProperty("total").GetInt32());
        Assert.False(next.TryGetProperty("total", out _));
    }

    [Theory]
    [InlineData("acme", new[] { "acme", "globex" })]
    [InlineData("GLO", new[] { "globex" })]
    [InlineData("aCmE hOl", new[] { "globex" })]
    [InlineData("  acme  ", new[] { "acme", "globex" })]
    [InlineData("istanbul İ", new[] { "istanbul" })]
    [InlineData("zzz", new string[0])]
    public async Task SearchMatchesTheStartOfTheIdentifierOrTheNameIgnoringCase(string q, string[] expected)
    {
        await using var platform = await PlatformAsync();
        await TenantSeed.InsertAsync(platform.Database, ("acme", "Acme Ltd"), ("globex", "Acme Holdings"), ("istanbul", "Istanbul İnc"), ("initech", "Initech"));

        var page = await platform.PageAsync($"?q={Uri.EscapeDataString(q)}&includeTotal=true");

        Assert.Equal(expected, Slugs(page));
        Assert.Equal(expected.Length, page.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task BlankSearchMeansNoFilter()
    {
        await using var platform = await PlatformAsync();
        await TenantSeed.InsertNumberedAsync(platform.Database, 3);

        var page = await platform.PageAsync("?q=%20%20&includeTotal=true");

        Assert.Equal(3, page.GetProperty("total").GetInt32());
    }

    [Theory]
    [InlineData("t-%")]
    [InlineData("t-_0001")]
    [InlineData("NAME _")]
    public async Task WildcardsInTheSearchAreLiteral(string q)
    {
        await using var platform = await PlatformAsync();
        await TenantSeed.InsertNumberedAsync(platform.Database, 3);

        var page = await platform.PageAsync($"?q={Uri.EscapeDataString(q)}");

        Assert.Empty(Slugs(page));
    }

    [Fact]
    public async Task SearchPagesFollowTheCursor()
    {
        await using var platform = await PlatformAsync();
        await TenantSeed.InsertNumberedAsync(platform.Database, 30);

        var first = await platform.PageAsync("?q=t-0001&size=4&includeTotal=true");
        var second = await platform.PageAsync($"?q=t-0001&size=4&after={first.GetProperty("nextCursor").GetString()}");
        var third = await platform.PageAsync($"?q=t-0001&size=4&after={second.GetProperty("nextCursor").GetString()}");

        Assert.Equal(10, first.GetProperty("total").GetInt32());
        Assert.Equal(Enumerable.Range(10, 10).Select(TenantSeed.Slug), [.. Slugs(first), .. Slugs(second), .. Slugs(third)]);
        Assert.Equal(JsonValueKind.Null, third.GetProperty("nextCursor").ValueKind);
    }

    [Theory]
    [InlineData("?size=0", "size")]
    [InlineData("?size=101", "size")]
    public async Task InvalidSizeIs400(string query, string field)
    {
        await using var platform = await PlatformAsync();

        await AssertValidationFailedAsync(await platform.GetAsync(query), field);
    }

    [Fact]
    public async Task SearchLongerThanANameIs400()
    {
        await using var platform = await PlatformAsync();

        await AssertValidationFailedAsync(await platform.GetAsync("?q=" + new string('a', 201)), "q");
        Assert.Equal(HttpStatusCode.OK, (await platform.GetAsync("?q=" + new string('a', 200))).StatusCode);
    }

    [Theory]
    [InlineData("broken!")]
    [InlineData("e30")]
    public async Task TamperedCursorIs422(string after)
    {
        await using var platform = await PlatformAsync();

        var response = await platform.GetAsync($"?after={after}");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("invalid-cursor", (await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("code").GetString());
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

    internal static string[] Slugs(JsonElement page) =>
        [.. page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("slug").GetString()!)];

    private static async Task AssertValidationFailedAsync(HttpResponseMessage response, string field)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        Assert.Equal("validation-failed", problem.GetProperty("code").GetString());
        Assert.True(problem.GetProperty("errors").TryGetProperty(field, out _), problem.ToString());
    }

    private async Task<PlatformList> PlatformAsync()
    {
        var database = await containers.Postgres.CreateDatabaseAsync(Cancellation);
        var app = await IdentityApp.StartAsync(containers, database);
        await app.WaitUntilMigratedAsync();
        return new PlatformList(app, database, await app.PlatformTokenAsync());
    }
}

internal sealed record PlatformList(IdentityApp App, string Database, string Token) : IAsyncDisposable
{
    private readonly HttpClient client = App.CreateClient();

    public Task<HttpResponseMessage> GetAsync(string query) => client.GetAsync("/tenants" + query, Token);

    public async Task<JsonElement> PageAsync(string query)
    {
        var response = await GetAsync(query);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        client.Dispose();
        await App.DisposeAsync();
    }
}

internal static class TenantSeed
{
    public static string Slug(int i) => $"t-{i:D5}";

    public static Task InsertAsync(string database, params (string Slug, string Name)[] tenants) =>
        ExecuteAsync(
            database,
            "INSERT INTO tenants (\"Id\", \"Slug\", \"Name\", \"NormalizedName\", \"CreatedAt\") VALUES "
            + string.Join(", ", tenants.Select(t => $"(md5(random()::text), '{t.Slug}', '{t.Name}', '{t.Name.ToUpperInvariant()}', now())")));

    public static Task InsertNumberedAsync(string database, int count) =>
        ExecuteAsync(
            database,
            "INSERT INTO tenants (\"Id\", \"Slug\", \"Name\", \"NormalizedName\", \"CreatedAt\")"
            + " SELECT md5(i::text), 't-' || lpad(i::text, 5, '0'), 'Name ' || i, 'NAME ' || i, now() FROM generate_series(1, "
            + count.ToString(System.Globalization.CultureInfo.InvariantCulture) + ") AS i;");

    private static async Task ExecuteAsync(string database, string sql)
    {
        await using var connection = new NpgsqlConnection(database);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(sql, connection) { CommandTimeout = 300 };
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }
}
