using Microsoft.IdentityModel.JsonWebTokens;

namespace Identity.Tests.Flow;

public sealed class IdentityTokenClaimsTests(ContainersFixture containers)
{
    private const string WithEmail = "openid offline_access email identity";

    [Fact]
    public async Task WithTheEmailScopeTheIdentityTokenCarriesUserTypeTenantAndEmail()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        var tenant = await app.CreateTenantAsync("acme");

        var tokens = await CodeFlow.SignInAsync(app, tenant.AdminEmail, IdentityApp.TenantAdminPassword, new AuthorizeRequest(WithEmail));

        var id = new JsonWebToken(tokens.IdToken);
        Assert.Equal("staff", id.GetClaim("user_type").Value);
        Assert.Equal(tenant.Id.ToString(), id.GetClaim("tenant_id").Value);
        Assert.Equal(tenant.AdminEmail, id.GetClaim("email").Value);
        var access = new JsonWebToken(tokens.AccessToken);
        Assert.Equal("staff", access.GetClaim("user_type").Value);
        Assert.Equal(tenant.Id.ToString(), access.GetClaim("tenant_id").Value);
        Assert.False(access.TryGetClaim("email", out _));
    }

    [Fact]
    public async Task WithoutTheEmailScopeThereIsNoEmailClaim()
    {
        await using var app = await IdentityApp.StartAsync(containers);

        var tokens = await CodeFlow.SignInAsync(app);

        var id = new JsonWebToken(tokens.IdToken);
        Assert.False(id.TryGetClaim("email", out _));
        Assert.Equal("platform", id.GetClaim("user_type").Value);
        Assert.False(id.TryGetClaim("tenant_id", out _));
    }

    [Fact]
    public async Task RefreshedIdentityTokenKeepsTheClaims()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        var tokens = await CodeFlow.SignInAsync(app, request: new AuthorizeRequest(WithEmail));
        using var client = app.CreateClient();

        var refreshed = await CodeFlow.RefreshAsync(client, tokens.RefreshToken!, new AuthorizeRequest(WithEmail));

        var id = new JsonWebToken(refreshed.IdToken);
        Assert.Equal(IdentityApp.AdminEmail, id.GetClaim("email").Value);
        Assert.Equal("platform", id.GetClaim("user_type").Value);
    }

    [Fact]
    public async Task StorefrontClientMayRequestEmail()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        var tenant = await app.CreateTenantAsync("shop");
        await app.CreateUserAsync("shopper@mail.test", "Shopper#1", tenant.Id.ToString(), Identity.Api.Users.UserTypes.Customer);

        var tokens = await CodeFlow.SignInAsync(app, "shopper@mail.test", "Shopper#1", AuthorizeRequest.Storefront("shop", WithEmail));

        var id = new JsonWebToken(tokens.IdToken);
        Assert.Equal("customer", id.GetClaim("user_type").Value);
        Assert.Equal("shopper@mail.test", id.GetClaim("email").Value);
    }
}
