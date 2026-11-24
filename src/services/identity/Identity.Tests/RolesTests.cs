using System.Net;
using System.Net.Http.Json;
using BuildingBlocks.Core.Identity;
using BuildingBlocks.Testing;
using Identity.Api.Authorization;
using Identity.Api.Roles;

namespace Identity.Tests;

public sealed class RolesTests(ContainersFixture containers)
{
    [Fact]
    public async Task AdministratorSeesFullCatalogueWithoutAssignFlag()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        using var client = app.CreateClient();
        var userId = Guid.NewGuid();
        await app.AssignAsync("acme", userId, IdentityRoles.Administrator);

        var response = await client.GetAsync("/roles", TestTokens.For(userId, "acme", UserType.Staff, IdentityApp.Audience));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<RolesResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.False(body.CanAssign);
        var expected = IdentityRoles.All.OrderBy(r => r.Key, StringComparer.Ordinal).Select(r => (r.Key, string.Join(',', r.Value.Order(StringComparer.Ordinal))));
        Assert.Equal(expected, body.Roles.Select(r => (r.Name, string.Join(',', r.Permissions))));
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
