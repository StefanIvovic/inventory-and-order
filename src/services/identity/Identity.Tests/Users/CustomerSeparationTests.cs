using System.Net;
using BuildingBlocks.Core.Identity;
using BuildingBlocks.Testing;
using Identity.Api.Authorization;
using Identity.Api.Users;

namespace Identity.Tests.Users;

public sealed class CustomerSeparationTests(ContainersFixture containers)
{
    [Fact]
    public async Task StaffListShowsNoCustomers()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        var tenant = await app.CreateTenantAsync("acme");
        await app.CreateUserAsync("shopper@mail.test", "Shopper#Pass1", tenant.Id, UserTypes.Customer);
        using var client = app.CreateClient();

        var body = await ListUsers.GetAsync(client, IdentityApp.StaffToken(tenant.AdminUserId, tenant.Id));

        Assert.Equal([tenant.AdminEmail], body.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("email").GetString()));
    }

    [Fact]
    public async Task StaffRoutesDoNotFindCustomers()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        var tenant = await app.CreateTenantAsync("acme");
        var customer = await app.CreateUserAsync("shopper@mail.test", "Shopper#Pass1", tenant.Id, UserTypes.Customer);
        using var client = app.CreateClient();
        var admin = IdentityApp.StaffToken(tenant.AdminUserId, tenant.Id);

        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync($"/users/{customer.Id}/disable", null, admin)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsync($"/users/{customer.Id}/roles/{IdentityRoles.Viewer}", admin)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/users/{customer.Id}/roles/{IdentityRoles.Viewer}", admin)).StatusCode);
    }

    [Fact]
    public async Task CustomerTokensAreForbiddenEverywhere()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        var tenant = await app.CreateTenantAsync("acme");
        var customer = await app.CreateUserAsync("shopper@mail.test", "Shopper#Pass1", tenant.Id, UserTypes.Customer);
        using var client = app.CreateClient();
        var token = TestTokens.For(customer.Id, tenant.Id, UserType.Customer, IdentityApp.Audience);
        var target = tenant.AdminUserId;

        HttpResponseMessage[] responses =
        [
            await client.GetAsync("/users", token),
            await client.GetAsync("/roles", token),
            await client.GetAsync("/tenants", token),
            await client.PostAsync("/users", new { email = "x@acme.test", password = "Staff#Pass1" }, token),
            await client.PostAsync("/tenants", new { }, token),
            await client.PostAsync($"/users/{target}/disable", null, token),
            await client.PutAsync($"/users/{target}/roles/{IdentityRoles.Viewer}", token),
            await client.DeleteAsync($"/users/{target}/roles/{IdentityRoles.Viewer}", token),
        ];

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode));
    }
}
