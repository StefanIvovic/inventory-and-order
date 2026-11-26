using System.Net;
using System.Net.Http.Json;
using BuildingBlocks.Core.Identity;
using BuildingBlocks.Testing;
using Identity.Api.Authorization;
using Identity.Api.Features.ListRoles;

namespace Identity.Tests;

public sealed class RolesTests(ContainersFixture containers)
{
    [Fact]
    public async Task AdministratorSeesTheTenantCatalogueAndMayAssign()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        using var client = app.CreateClient();
        var userId = Guid.NewGuid();
        await app.AssignAsync("acme", userId, IdentityRoles.Administrator);

        var response = await client.GetAsync("/roles", TestTokens.For(userId, "acme", UserType.Staff, IdentityApp.Audience));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<RolesResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.True(body.CanAssign);
        Assert.Equal(
            [
                "identity.administrator|Administrator|identity.roles.assign:Assign roles,identity.users.manage:Manage users,identity.roles.view:View roles,identity.users.view:View users",
                "identity.viewer|Viewer|identity.roles.view:View roles,identity.users.view:View users",
            ],
            body.Roles.Select(r => $"{r.Name}|{r.DisplayName}|{string.Join(',', r.Permissions.Select(p => $"{p.Name}:{p.DisplayName}"))}"));
    }

    [Fact]
    public async Task ViewerSeesTheCatalogueButMayNotAssign()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        using var client = app.CreateClient();
        var userId = Guid.NewGuid();
        await app.AssignAsync("acme", userId, IdentityRoles.Viewer);

        var response = await client.GetAsync("/roles", TestTokens.For(userId, "acme", UserType.Staff, IdentityApp.Audience));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<RolesResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.False(body.CanAssign);
    }

    [Fact]
    public async Task AssignmentInAnotherTenantDoesNotGrantAccess()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        using var client = app.CreateClient();
        var userId = Guid.NewGuid();
        await app.AssignAsync("globex", userId, IdentityRoles.Administrator);

        var response = await client.GetAsync("/roles", TestTokens.For(userId, "acme", UserType.Staff, IdentityApp.Audience));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains(IdentityPermissions.RolesView, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }
}
