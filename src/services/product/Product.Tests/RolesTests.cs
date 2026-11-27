using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BuildingBlocks.Core.Identity;
using BuildingBlocks.Testing;
using Product.Api.Authorization;

namespace Product.Tests;

public sealed class RolesTests(ContainersFixture containers)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task WithoutThePermissionTheAnswerIs403ListingIt()
    {
        await using var app = await MigratedAsync();

        var response = await GetAsync(app, ProductApp.StaffToken(Guid.NewGuid(), "acme"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        Assert.Equal(["product.roles.view"], problem.GetProperty("missingPermissions").EnumerateArray().Select(p => p.GetString()));
    }

    [Theory]
    [InlineData(ProductRoles.Viewer)]
    [InlineData(ProductRoles.Administrator)]
    public async Task ARoleThatGrantsViewingGetsTheCatalogue(string role)
    {
        await using var app = await MigratedAsync();
        var user = Guid.NewGuid();
        await app.AssignAsync("acme", user, role);

        var response = await GetAsync(app, ProductApp.StaffToken(user, "acme"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        var roles = body.GetProperty("roles").EnumerateArray().ToList();
        Assert.Equal(["product.administrator", "product.viewer"], roles.Select(r => r.GetProperty("name").GetString()));
        Assert.Equal(["Administrator", "Viewer"], roles.Select(r => r.GetProperty("displayName").GetString()));
        Assert.All(roles, r => Assert.Equal(
            [("product.roles.view", "View roles")],
            r.GetProperty("permissions").EnumerateArray().Select(p => (p.GetProperty("name").GetString(), p.GetProperty("displayName").GetString()))));
        Assert.False(body.GetProperty("canAssign").GetBoolean());
    }

    [Fact]
    public async Task AnAssignmentInAnotherTenantGrantsNothing()
    {
        await using var app = await MigratedAsync();
        var user = Guid.NewGuid();
        await app.AssignAsync("globex", user, ProductRoles.Viewer);

        var response = await GetAsync(app, ProductApp.StaffToken(user, "acme"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CustomersGetNoProductPermissions()
    {
        await using var app = await MigratedAsync();
        var user = Guid.NewGuid();
        await app.AssignAsync("acme", user, ProductRoles.Administrator);

        var response = await GetAsync(app, TestTokens.For(user, "acme", UserType.Customer, ProductApp.Audience));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public void EveryRoleAndPermissionHasADisplayName()
    {
        Assert.Equal(ProductRoles.Tenant.Keys.Order(StringComparer.Ordinal), ProductRoles.DisplayNames.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(ProductPermissions.All.Order(StringComparer.Ordinal), ProductPermissions.DisplayNames.Keys.Order(StringComparer.Ordinal));
        Assert.All(ProductRoles.Tenant.Values.SelectMany(p => p), permission => Assert.Contains(permission, ProductPermissions.All));
    }

    [Fact]
    public void TheAdministratorHoldsEveryPermissionAndTheViewerASubset()
    {
        Assert.Equal(ProductPermissions.All.Order(StringComparer.Ordinal), ProductRoles.Tenant[ProductRoles.Administrator].Order(StringComparer.Ordinal));
        Assert.Subset(ProductRoles.Tenant[ProductRoles.Administrator].ToHashSet(), ProductRoles.Tenant[ProductRoles.Viewer].ToHashSet());
    }

    private static async Task<HttpResponseMessage> GetAsync(ProductApp app, string token)
    {
        using var client = app.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/roles");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request, Cancellation);
    }

    private async Task<ProductApp> MigratedAsync()
    {
        var app = await ProductApp.StartAsync(containers);
        await app.WaitUntilMigratedAsync();
        return app;
    }
}
