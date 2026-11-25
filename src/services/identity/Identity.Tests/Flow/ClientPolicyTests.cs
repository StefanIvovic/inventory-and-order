using System.IdentityModel.Tokens.Jwt;
using System.Net;
using Identity.Api.Pages.Account;
using Identity.Api.Users;

namespace Identity.Tests.Flow;

public sealed class ClientPolicyTests(ContainersFixture containers)
{
    private const string CustomerPassword = "Customer#Pass1";

    [Theory]
    [InlineData(null)]
    [InlineData("no-such-shop")]
    public async Task StorefrontRequestWithoutAKnownTenantIsRejectedBeforeSignIn(string? tenant)
    {
        await using var app = await IdentityApp.StartAsync(containers);
        await app.WaitUntilMigratedAsync();
        using var browser = app.CreateBrowser();

        var response = await browser.GetAsync(CodeFlow.AuthorizeUrl(AuthorizeRequest.Storefront(tenant), CodeFlow.NewPkce().Challenge), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!;
        Assert.StartsWith(IdentityApp.StorefrontRedirectUri, location.ToString(), StringComparison.Ordinal);
        Assert.Equal("invalid_request", CodeFlow.Query(location, "error"));
    }

    [Fact]
    public async Task CustomerSignsInOnTheirStorefrontAndGetsACustomerToken()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        var acme = await app.CreateTenantAsync("acme");
        await app.CreateUserAsync("shopper@mail.test", CustomerPassword, acme.Id, UserTypes.Customer);

        var tokens = await CodeFlow.SignInAsync(app, "shopper@mail.test", CustomerPassword, AuthorizeRequest.Storefront("acme"));

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(tokens.AccessToken);
        Assert.Equal(acme.Id, jwt.Claims.Single(c => c.Type == "tenant_id").Value);
        Assert.Equal("customer", jwt.Claims.Single(c => c.Type == "user_type").Value);
    }

    [Fact]
    public async Task StaffOnTheStorefrontAndCustomersOnTheBackofficeGetTheGenericError()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        var acme = await app.CreateTenantAsync("acme");
        await app.CreateUserAsync("shopper@mail.test", CustomerPassword, acme.Id, UserTypes.Customer);

        await AssertGenericErrorAsync(app, AuthorizeRequest.Storefront("acme"), acme.AdminEmail, IdentityApp.TenantAdminPassword);
        await AssertGenericErrorAsync(app, AuthorizeRequest.Storefront("acme"), IdentityApp.AdminEmail, IdentityApp.AdminPassword);
        await AssertGenericErrorAsync(app, new AuthorizeRequest(), "shopper@mail.test", CustomerPassword);
    }

    [Fact]
    public async Task CustomerOfAnotherTenantGetsTheGenericError()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        await app.CreateTenantAsync("acme");
        var globex = await app.CreateTenantAsync("globex");
        await app.CreateUserAsync("shopper@mail.test", CustomerPassword, globex.Id, UserTypes.Customer);

        await AssertGenericErrorAsync(app, AuthorizeRequest.Storefront("acme"), "shopper@mail.test", CustomerPassword);
    }

    [Fact]
    public async Task AMismatchingSessionIsAskedToSignInAgain()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        var acme = await app.CreateTenantAsync("acme");
        await app.CreateTenantAsync("globex");
        await app.CreateUserAsync("shopper@mail.test", CustomerPassword, acme.Id, UserTypes.Customer);
        using var customer = app.CreateBrowser();
        await CodeFlow.AuthorizeAsync(customer, "shopper@mail.test", CustomerPassword, AuthorizeRequest.Storefront("acme"));
        using var staff = app.CreateBrowser();
        await CodeFlow.AuthorizeAsync(staff, acme.AdminEmail, IdentityApp.TenantAdminPassword);

        await AssertAskedToSignInAsync(customer, AuthorizeRequest.Storefront("globex"));
        await AssertAskedToSignInAsync(customer, new AuthorizeRequest());
        await AssertAskedToSignInAsync(staff, AuthorizeRequest.Storefront("acme"));
    }

    [Fact]
    public async Task BackofficeIgnoresTheTenantParameter()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        var acme = await app.CreateTenantAsync("acme");

        var tokens = await CodeFlow.SignInAsync(app, acme.AdminEmail, IdentityApp.TenantAdminPassword, new AuthorizeRequest(Tenant: "globex"));

        Assert.NotNull(tokens.AccessToken);
    }

    [Fact]
    public async Task CodesAndRefreshTokensWorkOnlyForTheClientThatGotThem()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        var acme = await app.CreateTenantAsync("acme");
        await app.CreateUserAsync("shopper@mail.test", CustomerPassword, acme.Id, UserTypes.Customer);
        using var browser = app.CreateBrowser();
        var storefront = AuthorizeRequest.Storefront("acme");
        var (code, verifier) = await CodeFlow.AuthorizeAsync(browser, "shopper@mail.test", CustomerPassword, storefront);

        var stolen = await CodeFlow.RedeemAsync(browser, code, verifier);
        var tokens = await CodeFlow.RedeemAsync(browser, code, verifier, request: storefront);
        var wrongRefresh = await CodeFlow.RefreshAsync(browser, tokens.RefreshToken!);

        Assert.Null(stolen.AccessToken);
        Assert.NotNull(tokens.AccessToken);
        Assert.Null(wrongRefresh.AccessToken);
        Assert.NotNull((await CodeFlow.RefreshAsync(browser, tokens.RefreshToken!, storefront)).AccessToken);
    }

    private static async Task AssertGenericErrorAsync(IdentityApp app, AuthorizeRequest request, string email, string password)
    {
        using var browser = app.CreateBrowser();
        var first = await browser.GetAsync(CodeFlow.AuthorizeUrl(request, CodeFlow.NewPkce().Challenge), TestContext.Current.CancellationToken);
        Assert.Contains("/account/login", first.Headers.Location!.ToString(), StringComparison.Ordinal);

        var login = await CodeFlow.LoginAsync(browser, first.Headers.Location!.ToString(), email, password);

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Contains(LoginModel.InvalidCredentials, await login.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    private static async Task AssertAskedToSignInAsync(HttpClient browser, AuthorizeRequest request)
    {
        var response = await browser.GetAsync(CodeFlow.AuthorizeUrl(request, CodeFlow.NewPkce().Challenge), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/account/login", response.Headers.Location!.ToString(), StringComparison.Ordinal);
    }
}
