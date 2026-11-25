using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Identity.Api.Authorization;
using Identity.Contracts;
using Identity.Tests.Tenants;
using Microsoft.EntityFrameworkCore;
using Wolverine.Tracking;

namespace Identity.Tests.Users;

public sealed class StaffUsersTests(ContainersFixture containers)
{
    internal const string StaffPassword = "Staff#Pass1";

    [Fact]
    public async Task AdministratorCreatesStaffWithNoRoles()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        var tenant = await app.CreateTenantAsync("acme");
        using var client = app.CreateClient();
        var token = IdentityApp.StaffToken(tenant.AdminUserId, tenant.Id);
        HttpResponseMessage? response = null;

        var session = await app.TrackAsync(async () => response = await client.PostAsync("/users", new { email = "Clerk@Acme.test", password = StaffPassword }, token));

        Assert.Equal(HttpStatusCode.Created, response!.StatusCode);
        var id = (await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("id").GetGuid();
        Assert.Equal($"/users/{id}", response.Headers.Location!.ToString());
        Assert.Equal(new UserRegistered(id, tenant.Id, "staff", "Clerk@Acme.test"), Assert.Single(session.Sent.MessagesOf<UserRegistered>()));
        var clerk = await ListUsers.FindAsync(client, token, id);
        Assert.Equal("staff", clerk.GetProperty("userType").GetString());
        Assert.False(clerk.GetProperty("disabled").GetBoolean());
        Assert.Empty(clerk.GetProperty("roles").EnumerateArray());
    }

    [Fact]
    public async Task EmailsOfPlatformAndOtherTenantsStaffAreTaken()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        var acme = await app.CreateTenantAsync("acme");
        await app.CreateTenantAsync("globex");
        using var client = app.CreateClient();
        var token = IdentityApp.StaffToken(acme.AdminUserId, acme.Id);

        await CreateTenantTests.AssertConflictAsync(await client.PostAsync("/users", new { email = IdentityApp.AdminEmail, password = StaffPassword }, token), "email-taken");
        await CreateTenantTests.AssertConflictAsync(await client.PostAsync("/users", new { email = "ADMIN@globex.test", password = StaffPassword }, token), "email-taken");
    }

    [Fact]
    public async Task ConcurrentCreationsWithTheSameEmailCreateOneUser()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        var tenant = await app.CreateTenantAsync("acme");
        using var client = app.CreateClient();
        var token = IdentityApp.StaffToken(tenant.AdminUserId, tenant.Id);

        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => client.PostAsync("/users", new { email = "clerk@acme.test", password = StaffPassword }, token)));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        foreach (var response in responses.Where(r => r.StatusCode != HttpStatusCode.Created))
        {
            await CreateTenantTests.AssertConflictAsync(response, "email-taken");
        }

        Assert.Equal(1, await CreateTenantTests.CountAsync(app, db => db.Users.CountAsync(u => u.Email == "clerk@acme.test", TestContext.Current.CancellationToken)));
    }

    [Theory]
    [InlineData("not-an-email", "short", new[] { "email", "password" })]
    [InlineData("clerk@acme.test", "longenough", new[] { "password" })]
    public async Task InvalidRequestsListTheirFieldErrors(string email, string password, string[] fields)
    {
        await using var app = await IdentityApp.StartAsync(containers);
        var tenant = await app.CreateTenantAsync("acme");
        using var client = app.CreateClient();

        var response = await client.PostAsync("/users", new { email, password }, IdentityApp.StaffToken(tenant.AdminUserId, tenant.Id));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("errors");
        Assert.Equal(fields, errors.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task ViewerCannotCreateEvenWithAnInvalidBody()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        var tenant = await app.CreateTenantAsync("acme");
        var viewer = await UserSetup.CreateStaffAsync(app, tenant, "viewer@acme.test", IdentityRoles.Viewer);
        using var client = app.CreateClient();

        var response = await client.PostAsync("/users", new { }, IdentityApp.StaffToken(viewer, tenant.Id));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ListShowsOnlyTheTenantsUsersByEmailWithActionFlags()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        var acme = await app.CreateTenantAsync("acme");
        await app.CreateTenantAsync("globex");
        var clerk = await UserSetup.CreateStaffAsync(app, acme, "clerk@acme.test");
        var viewer = await UserSetup.CreateStaffAsync(app, acme, "viewer@acme.test", IdentityRoles.Viewer);
        using var client = app.CreateClient();
        var adminToken = IdentityApp.StaffToken(acme.AdminUserId, acme.Id);

        var body = await ListUsers.GetAsync(client, adminToken);
        var items = body.GetProperty("items").EnumerateArray().ToList();

        Assert.Equal(["admin@acme.test", "clerk@acme.test", "viewer@acme.test"], items.Select(i => i.GetProperty("email").GetString()));
        Assert.True(body.GetProperty("actions").GetProperty("canCreate").GetBoolean());
        Assert.Equal([IdentityRoles.Administrator], items[0].GetProperty("roles").EnumerateArray().Select(r => r.GetString()));
        Assert.False(items[0].GetProperty("actions").GetProperty("canDisable").GetBoolean());
        Assert.True(items[0].GetProperty("actions").GetProperty("canAssignRoles").GetBoolean());
        Assert.True(items[1].GetProperty("actions").GetProperty("canDisable").GetBoolean());

        var viewerBody = await ListUsers.GetAsync(client, IdentityApp.StaffToken(viewer, acme.Id));
        Assert.False(viewerBody.GetProperty("actions").GetProperty("canCreate").GetBoolean());
        Assert.All(viewerBody.GetProperty("items").EnumerateArray(), item =>
        {
            Assert.False(item.GetProperty("actions").GetProperty("canDisable").GetBoolean());
            Assert.False(item.GetProperty("actions").GetProperty("canAssignRoles").GetBoolean());
        });

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/users", IdentityApp.StaffToken(clerk, acme.Id))).StatusCode);
    }
}

internal static class ListUsers
{
    public static async Task<JsonElement> GetAsync(HttpClient client, string token)
    {
        var response = await client.GetAsync("/users", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
    }

    public static async Task<JsonElement> FindAsync(HttpClient client, string token, Guid id) =>
        (await GetAsync(client, token)).GetProperty("items").EnumerateArray().Single(i => i.GetProperty("id").GetGuid() == id);
}

internal static class UserSetup
{
    public static async Task<Guid> CreateStaffAsync(IdentityApp app, CreatedTenant tenant, string email, string? role = null)
    {
        using var client = app.CreateClient();
        var token = IdentityApp.StaffToken(tenant.AdminUserId, tenant.Id);
        var response = await client.PostAsync("/users", new { email, password = StaffUsersTests.StaffPassword }, token);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var id = (await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("id").GetGuid();
        if (role is not null)
        {
            Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsync($"/users/{id}/roles/{role}", token)).StatusCode);
        }

        return id;
    }
}
