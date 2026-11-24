using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BuildingBlocks.Messaging;
using Identity.Api.Authorization;
using Identity.Tests.Tenants;
using Wolverine.Tracking;

namespace Identity.Tests.Users;

public sealed class RoleAssignmentTests(ContainersFixture containers)
{
    [Fact]
    public async Task AssignedViewerMayListButNotCreateAndLosesItOnUnassign()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        var tenant = await app.CreateTenantAsync("acme");
        var clerk = await UserSetup.CreateStaffAsync(app, tenant, "clerk@acme.test");
        using var client = app.CreateClient();
        var admin = IdentityApp.StaffToken(tenant.AdminUserId, tenant.Id);
        var clerkToken = IdentityApp.StaffToken(clerk, tenant.Id);
        HttpResponseMessage? assigned = null;

        var session = await app.TrackAsync(async () => assigned = await client.PutAsync($"/users/{clerk}/roles/{IdentityRoles.Viewer}", admin));

        Assert.Equal(HttpStatusCode.NoContent, assigned!.StatusCode);
        Assert.Single(session.Sent.MessagesOf<PermissionAssignmentsChanged>());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/users", clerkToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/users", new { email = "x@acme.test", password = StaffUsersTests.StaffPassword }, clerkToken)).StatusCode);

        await app.TrackAsync(() => client.DeleteAsync($"/users/{clerk}/roles/{IdentityRoles.Viewer}", admin));

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/users", clerkToken)).StatusCode);
    }

    [Fact]
    public async Task RepeatingAnAssignmentOrRemovalChangesNothing()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        var tenant = await app.CreateTenantAsync("acme");
        var clerk = await UserSetup.CreateStaffAsync(app, tenant, "clerk@acme.test", IdentityRoles.Viewer);
        using var client = app.CreateClient();
        var admin = IdentityApp.StaffToken(tenant.AdminUserId, tenant.Id);
        HttpResponseMessage? again = null;
        HttpResponseMessage? missing = null;

        var assignSession = await app.TrackAsync(async () => again = await client.PutAsync($"/users/{clerk}/roles/{IdentityRoles.Viewer}", admin));
        var removeSession = await app.TrackAsync(async () => missing = await client.DeleteAsync($"/users/{clerk}/roles/{IdentityRoles.Administrator}", admin));

        Assert.Equal(HttpStatusCode.NoContent, again!.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, missing!.StatusCode);
        Assert.Empty(assignSession.Sent.MessagesOf<PermissionAssignmentsChanged>());
        Assert.Empty(removeSession.Sent.MessagesOf<PermissionAssignmentsChanged>());
        Assert.Equal([IdentityRoles.Viewer], (await ListUsers.FindAsync(client, admin, clerk)).GetProperty("roles").EnumerateArray().Select(r => r.GetString()));
    }

    [Theory]
    [InlineData("identity.unknown")]
    [InlineData(IdentityRoles.PlatformAdministrator)]
    public async Task UnknownOrPlatformRoleIsAFieldError(string role)
    {
        await using var app = await IdentityApp.StartAsync(containers);
        var tenant = await app.CreateTenantAsync("acme");
        var clerk = await UserSetup.CreateStaffAsync(app, tenant, "clerk@acme.test");
        using var client = app.CreateClient();

        var response = await client.PutAsync($"/users/{clerk}/roles/{role}", IdentityApp.StaffToken(tenant.AdminUserId, tenant.Id));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("errors");
        Assert.Equal(["role"], errors.EnumerateObject().Select(p => p.Name));
    }

    [Fact]
    public async Task RemovingYourOwnAdministratorRoleIsRefused()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        var tenant = await app.CreateTenantAsync("acme");
        using var client = app.CreateClient();
        var admin = IdentityApp.StaffToken(tenant.AdminUserId, tenant.Id);

        var response = await client.DeleteAsync($"/users/{tenant.AdminUserId}/roles/{IdentityRoles.Administrator}", admin);

        await CreateTenantTests.AssertConflictAsync(response, "cannot-remove-own-admin");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/users", admin)).StatusCode);
    }

    [Fact]
    public async Task AssigningToADisabledUserIsAConflict()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        var tenant = await app.CreateTenantAsync("acme");
        var clerk = await UserSetup.CreateStaffAsync(app, tenant, "clerk@acme.test");
        using var client = app.CreateClient();
        var admin = IdentityApp.StaffToken(tenant.AdminUserId, tenant.Id);
        await app.TrackAsync(() => client.PostAsync($"/users/{clerk}/disable", null, admin));

        var response = await client.PutAsync($"/users/{clerk}/roles/{IdentityRoles.Viewer}", admin);

        await CreateTenantTests.AssertConflictAsync(response, "user-disabled");
    }

    [Fact]
    public async Task UsersOfAnotherTenantAreNotFound()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        var acme = await app.CreateTenantAsync("acme");
        var globex = await app.CreateTenantAsync("globex");
        using var client = app.CreateClient();

        var response = await client.PutAsync($"/users/{globex.AdminUserId}/roles/{IdentityRoles.Viewer}", IdentityApp.StaffToken(acme.AdminUserId, acme.Id));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ViewerCannotAssignEvenAnUnknownRole()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        var tenant = await app.CreateTenantAsync("acme");
        var viewer = await UserSetup.CreateStaffAsync(app, tenant, "viewer@acme.test", IdentityRoles.Viewer);
        using var client = app.CreateClient();

        var response = await client.PutAsync($"/users/{viewer}/roles/not-a-role", IdentityApp.StaffToken(viewer, tenant.Id));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
