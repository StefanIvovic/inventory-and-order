using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Identity.Api.Authorization;
using Identity.Api.Users;
using Npgsql;

namespace Identity.Tests.Users;

public sealed class StaffListPagingTests(ContainersFixture containers)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task DefaultPagesAreSortedByEmailAndWalkEveryUserOnce()
    {
        await using var acme = await AcmeAsync(30);

        var first = await acme.PageAsync(string.Empty);
        var second = await acme.PageAsync($"?after={first.GetProperty("nextCursor").GetString()}");

        Assert.Equal(25, Emails(first).Length);
        Assert.Equal("admin@acme.test", Emails(first)[0]);
        Assert.Equal(["admin@acme.test", .. Enumerable.Range(1, 30).Select(Email)], [.. Emails(first), .. Emails(second)]);
        Assert.Equal(JsonValueKind.Null, second.GetProperty("nextCursor").ValueKind);
        Assert.False(first.TryGetProperty("total", out _));
    }

    [Fact]
    public async Task SmallPagesFollowTheCursorWithoutGapsOrRepeats()
    {
        await using var acme = await AcmeAsync(12);
        var seen = new List<string>();
        string? after = null;

        do
        {
            var page = await acme.PageAsync(after is null ? "?size=5" : $"?size=5&after={after}");
            seen.AddRange(Emails(page));
            after = page.GetProperty("nextCursor").GetString();
        }
        while (after is not null);

        Assert.Equal(["admin@acme.test", .. Enumerable.Range(1, 12).Select(Email)], seen);
    }

    [Theory]
    [InlineData("?q=u-001", new[] { 10, 11, 12, 13, 14, 15, 16, 17, 18, 19 })]
    [InlineData("?q=U-001", new[] { 10, 11, 12, 13, 14, 15, 16, 17, 18, 19 })]
    [InlineData("?status=disabled", new[] { 10, 20, 30 })]
    [InlineData("?role=identity.viewer", new[] { 1, 2, 3, 4, 5 })]
    [InlineData("?q=u-002&status=disabled", new[] { 20 })]
    [InlineData("?role=identity.viewer&status=active&q=u-000", new[] { 1, 2, 3, 4, 5 })]
    [InlineData("?q=nobody", new int[0])]
    public async Task FiltersCombine(string query, int[] expected)
    {
        await using var acme = await AcmeAsync(30);

        var page = await acme.PageAsync(query);

        Assert.Equal(expected.Select(Email), Emails(page));
    }

    [Fact]
    public async Task ActiveStatusAndAdministratorRoleFindTheAdmin()
    {
        await using var acme = await AcmeAsync(3);

        var page = await acme.PageAsync("?status=active&role=identity.administrator");

        Assert.Equal(["admin@acme.test"], Emails(page));
    }

    [Theory]
    [InlineData("?size=0", "size")]
    [InlineData("?size=101", "size")]
    [InlineData("?status=any", "status")]
    [InlineData("?role=identity.platform-administrator", "role")]
    [InlineData("?role=nope", "role")]
    public async Task InvalidFiltersAre400(string query, string field)
    {
        await using var acme = await AcmeAsync(1);

        var response = await acme.GetAsync(query);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        Assert.Equal("validation-failed", problem.GetProperty("code").GetString());
        Assert.True(problem.GetProperty("errors").TryGetProperty(field, out _), problem.ToString());
    }

    [Fact]
    public async Task LongSearchIs400()
    {
        await using var acme = await AcmeAsync(1);

        var response = await acme.GetAsync("?q=" + new string('a', 257));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("broken!")]
    [InlineData("e30")]
    public async Task TamperedCursorIs422(string after)
    {
        await using var acme = await AcmeAsync(1);

        var response = await acme.GetAsync($"?after={after}");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("invalid-cursor", (await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task TotalIsCountedOnlyWhenAsked()
    {
        await using var acme = await AcmeAsync(30);

        var all = await acme.PageAsync("?includeTotal=true&size=5");
        var disabled = await acme.PageAsync("?includeTotal=true&status=disabled");

        Assert.Equal(31, all.GetProperty("total").GetInt32());
        Assert.Equal(3, disabled.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task FlagsFollowPermissionAndState()
    {
        await using var acme = await AcmeAsync(10);

        var page = await acme.PageAsync("?q=admin");
        var disabled = await acme.PageAsync("?status=disabled");
        var active = await acme.PageAsync("?q=u-0001");

        Assert.True(page.GetProperty("actions").GetProperty("canCreate").GetBoolean());
        Assert.Equal((false, true), Actions(page.GetProperty("items")[0]));
        Assert.Equal((false, false), Actions(disabled.GetProperty("items")[0]));
        Assert.Equal((true, true), Actions(active.GetProperty("items")[0]));
        Assert.Equal(["identity.viewer"], active.GetProperty("items")[0].GetProperty("roles").EnumerateArray().Select(r => r.GetString()));
    }

    [Fact]
    public async Task OtherTenantsStaffAndCustomersNeverAppear()
    {
        await using var acme = await AcmeAsync(2);
        var globex = await acme.App.CreateTenantAsync("globex");
        await acme.App.CreateUserAsync("clerk@globex.test", StaffUsersTests.StaffPassword, globex.Id);
        await acme.App.CreateUserAsync("u-0001@shop.test", "Shopper#1", acme.Tenant.Id, UserTypes.Customer);

        var page = await acme.PageAsync("?includeTotal=true");

        Assert.Equal(["admin@acme.test", Email(1), Email(2)], Emails(page));
        Assert.Equal(3, page.GetProperty("total").GetInt32());
    }

    internal static string Email(int i) => $"u-{i:D4}@acme.test";

    internal static string[] Emails(JsonElement page) =>
        [.. page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("email").GetString()!)];

    private static (bool CanDisable, bool CanAssignRoles) Actions(JsonElement item) =>
        (item.GetProperty("actions").GetProperty("canDisable").GetBoolean(), item.GetProperty("actions").GetProperty("canAssignRoles").GetBoolean());

    private async Task<StaffTenant> AcmeAsync(int staff)
    {
        var database = await containers.Postgres.CreateDatabaseAsync(Cancellation);
        var app = await IdentityApp.StartAsync(containers, database);
        var tenant = await app.CreateTenantAsync("acme");
        await StaffSeed.InsertAsync(database, tenant.Id, "acme.test", staff, disabledEvery: 10, viewers: 5);
        return new StaffTenant(app, tenant, database);
    }
}

internal sealed record StaffTenant(IdentityApp App, CreatedTenant Tenant, string Database) : IAsyncDisposable
{
    private readonly HttpClient client = App.CreateClient();

    public Task<HttpResponseMessage> GetAsync(string query) =>
        client.GetAsync("/users" + query, IdentityApp.StaffToken(Tenant.AdminUserId, Tenant.Id));

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

internal static class StaffSeed
{
    public static async Task InsertAsync(string database, string tenantId, string domain, int count, int disabledEvery = 0, int viewers = 0, int viewerEvery = 0)
    {
        if (count == 0)
        {
            return;
        }

        var digits = Math.Max(4, count.ToString(System.Globalization.CultureInfo.InvariantCulture).Length);
        var email = $"'u-' || lpad(i::text, {digits}, '0') || '@{domain}'";
        var disabled = disabledEvery > 0 ? $"CASE WHEN i % {disabledEvery} = 0 THEN now() END" : "NULL";
        var sql =
            "INSERT INTO identity_users (\"Id\", \"UserTypeId\", \"TenantId\", \"CreatedAt\", \"DisabledAt\", \"UserName\", \"NormalizedUserName\","
            + " \"Email\", \"NormalizedEmail\", \"EmailConfirmed\", \"SecurityStamp\", \"ConcurrencyStamp\", \"PhoneNumberConfirmed\","
            + " \"TwoFactorEnabled\", \"LockoutEnabled\", \"AccessFailedCount\")"
            + $" SELECT gen_random_uuid(), '{UserTypes.StaffValue.Id}', '{tenantId}', now(), {disabled}, {email}, upper({email}),"
            + $" {email}, upper({email}), true, md5(i::text), md5(i::text), false, false, true, 0 FROM generate_series(1, {count}) AS i;"
            + " INSERT INTO role_assignments (\"TenantId\", \"UserId\", \"Role\", \"AssignedAt\")"
            + $" SELECT '{tenantId}', \"Id\", '{IdentityRoles.Viewer}', now() FROM identity_users"
            + $" WHERE \"TenantId\" = '{tenantId}' AND \"Email\" LIKE 'u-%' AND {ViewerCondition(digits, viewers, viewerEvery)};";
        await using var connection = new NpgsqlConnection(database);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(sql, connection) { CommandTimeout = 300 };
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private static string ViewerCondition(int digits, int viewers, int viewerEvery)
    {
        var number = $"substring(\"Email\" from 3 for {digits})::int";
        return viewerEvery > 0 ? $"{number} % {viewerEvery} = 1" : $"{number} <= {viewers}";
    }
}
