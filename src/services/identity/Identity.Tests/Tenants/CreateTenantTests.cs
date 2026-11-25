using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BuildingBlocks.Core.Identity;
using BuildingBlocks.Testing;
using Identity.Api.Persistence;
using Identity.Contracts;
using Identity.Tests.Flow;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine.Tracking;

namespace Identity.Tests.Tenants;

public sealed class CreateTenantTests(ContainersFixture containers)
{
    [Fact]
    public async Task PlatformAdminCreatesATenantWhoseAdminSignsInAndAdministers()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        CreatedTenant? tenant = null;

        var session = await app.TrackAsync(async () => tenant = await app.CreateTenantAsync("acme"));

        Assert.NotNull(tenant);
        var created = Assert.Single(session.Sent.MessagesOf<TenantCreated>());
        Assert.Equal(new TenantCreated(tenant.Id, tenant.AdminUserId), created);
        var registered = Assert.Single(session.Sent.MessagesOf<UserRegistered>());
        Assert.Equal(new UserRegistered(tenant.AdminUserId, tenant.Id, "staff", tenant.AdminEmail), registered);

        var tokens = await CodeFlow.SignInAsync(app, tenant.AdminEmail, IdentityApp.TenantAdminPassword);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(tokens.AccessToken);
        Assert.Equal(tenant.Id, jwt.Claims.Single(c => c.Type == "tenant_id").Value);
        Assert.Equal("staff", jwt.Claims.Single(c => c.Type == "user_type").Value);
        using var client = app.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/users", tokens.AccessToken)).StatusCode);
    }

    [Fact]
    public async Task DuplicateSlugIsAConflictAndChangesNothing()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        await app.CreateTenantAsync("acme");
        using var client = app.CreateClient();
        var token = await app.PlatformTokenAsync();
        HttpResponseMessage? response = null;

        var session = await app.TrackAsync(async () => response = await client.PostAsync("/tenants", Body("acme", "other@acme.test"), token));

        await AssertConflictAsync(response!, "tenant-slug-taken");
        Assert.Empty(session.Sent.MessagesOf<TenantCreated>());
        Assert.Equal(1, await CountAsync(app, db => db.Tenants.CountAsync(TestContext.Current.CancellationToken)));
        Assert.Equal(0, await CountAsync(app, db => db.Users.CountAsync(u => u.Email == "other@acme.test", TestContext.Current.CancellationToken)));
    }

    [Fact]
    public async Task EmailOfAnyStaffOrPlatformUserIsAConflict()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        await app.CreateTenantAsync("acme");
        using var client = app.CreateClient();
        var token = await app.PlatformTokenAsync();

        await AssertConflictAsync(await client.PostAsync("/tenants", Body("globex", IdentityApp.AdminEmail.ToUpperInvariant()), token), "email-taken");
        await AssertConflictAsync(await client.PostAsync("/tenants", Body("initech", "admin@acme.test"), token), "email-taken");

        Assert.Equal(1, await CountAsync(app, db => db.Tenants.CountAsync(TestContext.Current.CancellationToken)));
    }

    [Fact]
    public async Task ConcurrentCreationsWithTheSameSlugCreateOneTenant()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        using var client = app.CreateClient();
        var token = await app.PlatformTokenAsync();

        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(i => client.PostAsync("/tenants", Body("acme", $"admin{i}@acme.test"), token)));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        foreach (var response in responses.Where(r => r.StatusCode != HttpStatusCode.Created))
        {
            await AssertConflictAsync(response, "tenant-slug-taken");
        }

        Assert.Equal(1, await CountAsync(app, db => db.Tenants.CountAsync(TestContext.Current.CancellationToken)));
        Assert.Equal(1, await CountAsync(app, db => db.Users.CountAsync(u => u.UserTypeId == Identity.Api.Users.UserTypes.StaffValue.Id, TestContext.Current.CancellationToken)));
    }

    [Fact]
    public async Task ConcurrentCreationsWithTheSameAdminEmailCreateOneTenant()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        using var client = app.CreateClient();
        var token = await app.PlatformTokenAsync();

        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(i => client.PostAsync("/tenants", Body($"tenant-{i}", "boss@shared.test"), token)));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        foreach (var response in responses.Where(r => r.StatusCode != HttpStatusCode.Created))
        {
            await AssertConflictAsync(response, "email-taken");
        }

        Assert.Equal(1, await CountAsync(app, db => db.Tenants.CountAsync(TestContext.Current.CancellationToken)));
    }

    [Fact]
    public async Task InvalidRequestListsEveryFieldError()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        using var client = app.CreateClient();

        var response = await client.PostAsync(
            "/tenants",
            new { slug = "Bad--Slug-", name = " ", adminEmail = "not-an-email", adminPassword = "short" },
            await app.PlatformTokenAsync());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("errors");
        Assert.Equal(["adminEmail", "adminPassword", "name", "slug"], errors.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
        Assert.Equal(0, await CountAsync(app, db => db.Tenants.CountAsync(TestContext.Current.CancellationToken)));
    }

    [Fact]
    public async Task WeakPasswordIsAFieldErrorOnThePassword()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        using var client = app.CreateClient();

        var response = await client.PostAsync("/tenants", Body("acme", "admin@acme.test", "longenough"), await app.PlatformTokenAsync());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("errors");
        Assert.Equal(["adminPassword"], errors.EnumerateObject().Select(p => p.Name));
        Assert.Equal(0, await CountAsync(app, db => db.Tenants.CountAsync(TestContext.Current.CancellationToken)));
    }

    [Fact]
    public async Task OnlyPlatformUsersWithThePlatformRoleMayCreate()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        await app.WaitUntilMigratedAsync();
        using var client = app.CreateClient();

        var withoutRole = await client.PostAsync("/tenants", Body("acme", "admin@acme.test"), TestTokens.Platform(Guid.NewGuid(), IdentityApp.Audience));
        var tenantUser = await client.PostAsync("/tenants", Body("acme", "admin@acme.test"), TestTokens.For(Guid.NewGuid(), "acme", UserType.Staff, IdentityApp.Audience));

        Assert.Equal(HttpStatusCode.Forbidden, withoutRole.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, tenantUser.StatusCode);
        Assert.Equal(0, await CountAsync(app, db => db.Tenants.CountAsync(TestContext.Current.CancellationToken)));
    }

    internal static object Body(string slug, string adminEmail, string adminPassword = IdentityApp.TenantAdminPassword) =>
        new { slug, name = slug + " Ltd", adminEmail, adminPassword };

    internal static async Task AssertConflictAsync(HttpResponseMessage response, string code)
    {
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(code, body.GetProperty("code").GetString());
    }

    internal static async Task<int> CountAsync(IdentityApp app, Func<IdentityDbContext, Task<int>> query)
    {
        await using var scope = app.Services.CreateAsyncScope();
        return await query(scope.ServiceProvider.GetRequiredService<IdentityDbContext>());
    }
}
