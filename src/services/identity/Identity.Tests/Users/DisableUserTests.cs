using System.Net;
using BuildingBlocks.Messaging;
using Identity.Api.Authorization;
using Identity.Contracts;
using Identity.Tests.Tenants;
using Wolverine.Tracking;

namespace Identity.Tests.Users;

public sealed class DisableUserTests(ContainersFixture containers)
{
    [Fact]
    public async Task DisablingMarksTheUserChangesTheStampAndPublishesOnce()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        var tenant = await app.CreateTenantAsync("acme");
        var clerk = await UserSetup.CreateStaffAsync(app, tenant, "clerk@acme.test");
        var stamp = (await app.FindUserAsync("clerk@acme.test")).SecurityStamp;
        using var client = app.CreateClient();
        var token = IdentityApp.StaffToken(tenant.AdminUserId, tenant.Id);
        HttpResponseMessage? first = null;
        HttpResponseMessage? second = null;

        var firstSession = await app.TrackAsync(async () => first = await client.PostAsync($"/users/{clerk}/disable", null, token));
        var secondSession = await app.TrackAsync(async () => second = await client.PostAsync($"/users/{clerk}/disable", null, token));

        Assert.Equal(HttpStatusCode.NoContent, first!.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, second!.StatusCode);
        Assert.Equal(new UserDisabled(clerk, tenant.Id), Assert.Single(firstSession.Sent.MessagesOf<UserDisabled>()));
        Assert.Single(firstSession.Sent.MessagesOf<PermissionAssignmentsChanged>());
        Assert.Empty(secondSession.Sent.MessagesOf<UserDisabled>());
        var user = await app.FindUserAsync("clerk@acme.test");
        Assert.True(user.IsDisabled);
        Assert.NotEqual(stamp, user.SecurityStamp);
        Assert.True((await ListUsers.FindAsync(client, token, clerk)).GetProperty("disabled").GetBoolean());
    }

    [Fact]
    public async Task DisablingYourselfIsRefused()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        var tenant = await app.CreateTenantAsync("acme");
        using var client = app.CreateClient();

        var response = await client.PostAsync($"/users/{tenant.AdminUserId}/disable", null, IdentityApp.StaffToken(tenant.AdminUserId, tenant.Id));

        await CreateTenantTests.AssertConflictAsync(response, "cannot-disable-self");
        Assert.False((await app.FindUserAsync(tenant.AdminEmail)).IsDisabled);
    }

    [Fact]
    public async Task UsersOfAnotherTenantAreNotFound()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        var acme = await app.CreateTenantAsync("acme");
        var globex = await app.CreateTenantAsync("globex");
        using var client = app.CreateClient();
        var token = IdentityApp.StaffToken(acme.AdminUserId, acme.Id);

        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync($"/users/{globex.AdminUserId}/disable", null, token)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync($"/users/{Guid.NewGuid()}/disable", null, token)).StatusCode);
        Assert.False((await app.FindUserAsync(globex.AdminEmail)).IsDisabled);
    }

    [Fact]
    public async Task ViewerCannotDisable()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        var tenant = await app.CreateTenantAsync("acme");
        var viewer = await UserSetup.CreateStaffAsync(app, tenant, "viewer@acme.test", IdentityRoles.Viewer);
        using var client = app.CreateClient();

        var response = await client.PostAsync($"/users/{tenant.AdminUserId}/disable", null, IdentityApp.StaffToken(viewer, tenant.Id));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DisabledUserLosesPermissionsWithinSeconds()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        var tenant = await app.CreateTenantAsync("acme");
        var viewer = await UserSetup.CreateStaffAsync(app, tenant, "viewer@acme.test", IdentityRoles.Viewer);
        using var client = app.CreateClient();
        var viewerToken = IdentityApp.StaffToken(viewer, tenant.Id);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/users", viewerToken)).StatusCode);

        await app.TrackAsync(() => client.PostAsync($"/users/{viewer}/disable", null, IdentityApp.StaffToken(tenant.AdminUserId, tenant.Id)));

        await Eventually.AssertAsync(
            async () => (await client.GetAsync("/users", viewerToken)).StatusCode == HttpStatusCode.Forbidden,
            "the disabled viewer loses GET /users");
    }
}
