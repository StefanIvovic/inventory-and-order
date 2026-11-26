using System.IdentityModel.Tokens.Jwt;
using System.Net;
using Identity.Api.Pages.Account;
using Identity.Api.Users;
using Identity.Contracts;
using Identity.Tests.Tenants;
using Microsoft.EntityFrameworkCore;
using Wolverine.Tracking;

namespace Identity.Tests.Flow;

public sealed class RegistrationTests(ContainersFixture containers)
{
    private const string Password = "Shopper#Pass1";

    [Fact]
    public async Task ShopperRegistersFromTheStorefrontLoginAndComesBackSignedIn()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        var acme = await app.CreateTenantAsync("acme");
        using var browser = app.CreateBrowser();
        var request = AuthorizeRequest.Storefront("acme");
        var (verifier, challenge) = CodeFlow.NewPkce();
        var toLogin = await browser.GetAsync(CodeFlow.AuthorizeUrl(request, challenge), TestContext.Current.CancellationToken);
        var loginPage = await browser.GetStringAsync(toLogin.Headers.Location!, TestContext.Current.CancellationToken);
        var registerUrl = CodeFlow.RegisterLink(loginPage);
        var registerPage = await browser.GetStringAsync(registerUrl, TestContext.Current.CancellationToken);
        Assert.Contains("acme Ltd", loginPage, StringComparison.Ordinal);
        Assert.Contains("acme Ltd", registerPage, StringComparison.Ordinal);
        HttpResponseMessage? registered = null;

        var session = await app.TrackAsync(async () => registered = await CodeFlow.RegisterAsync(browser, registerUrl, "Shopper@Mail.test ", Password));

        Assert.Equal(HttpStatusCode.Redirect, registered!.StatusCode);
        var back = await browser.GetAsync(registered.Headers.Location!, TestContext.Current.CancellationToken);
        var code = CodeFlow.Query(back.Headers.Location!, "code");
        Assert.NotNull(code);
        var tokens = await CodeFlow.RedeemAsync(browser, code, verifier, request: request);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(tokens.AccessToken);
        Assert.Equal(acme.Id, jwt.Claims.Single(c => c.Type == "tenant_id").Value);
        Assert.Equal("customer", jwt.Claims.Single(c => c.Type == "user_type").Value);
        var envelope = Assert.Single(session.Sent.Envelopes(), e => e.Message is UserRegistered);
        Assert.Equal(new UserRegistered(Guid.Parse(jwt.Subject), acme.Id, "customer", "Shopper@Mail.test"), envelope.Message);
        Assert.Equal(acme.Id, envelope.TenantId);
    }

    [Fact]
    public async Task InvalidInputShowsEveryFieldErrorAndCreatesNothing()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        await app.CreateTenantAsync("acme");
        using var browser = app.CreateBrowser();

        var mismatch = await CodeFlow.RegisterAsync(browser, "/account/register?tenant=acme", "not-an-email", "short", "different");
        var weak = await CodeFlow.RegisterAsync(browser, "/account/register?tenant=acme", "shopper@mail.test", "longenough");

        Assert.Equal(HttpStatusCode.OK, mismatch.StatusCode);
        var page = await mismatch.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("data-error=\"email\"", page, StringComparison.Ordinal);
        Assert.Contains("data-error=\"password\"", page, StringComparison.Ordinal);
        Assert.Contains("data-error=\"confirmPassword\"", page, StringComparison.Ordinal);
        Assert.Contains("data-error=\"password\"", await weak.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        Assert.Equal(0, await CustomersAsync(app));
    }

    [Fact]
    public async Task ExistingCustomerEmailInTheSameTenantIsRefusedWithASignInLink()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        await app.CreateTenantAsync("acme");
        using var browser = app.CreateBrowser();
        await CodeFlow.RegisterAsync(browser, "/account/register?tenant=acme", "shopper@mail.test", Password);
        using var other = app.CreateBrowser();

        var again = await CodeFlow.RegisterAsync(other, "/account/register?tenant=acme", "SHOPPER@mail.test", "Other#Pass1");

        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        var page = await again.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains(RegisterModel.AlreadyExists, page, StringComparison.Ordinal);
        Assert.Contains("href=\"/account/login", page, StringComparison.Ordinal);
        Assert.Equal(1, await CustomersAsync(app));
    }

    [Fact]
    public async Task TheSameEmailRegistersInAnotherTenantAndBesideAStaffAccount()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        var acme = await app.CreateTenantAsync("acme");
        await app.CreateTenantAsync("globex");
        using var first = app.CreateBrowser();
        using var second = app.CreateBrowser();

        var atAcme = await CodeFlow.RegisterAsync(first, "/account/register?tenant=acme", acme.AdminEmail, Password);
        var atGlobex = await CodeFlow.RegisterAsync(second, "/account/register?tenant=globex", acme.AdminEmail, Password);

        Assert.Equal(HttpStatusCode.Redirect, atAcme.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, atGlobex.StatusCode);
        Assert.Equal(2, await CustomersAsync(app));
    }

    [Fact]
    public async Task ConcurrentRegistrationsWithOneEmailCreateOneAccount()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        await app.CreateTenantAsync("acme");
        var browsers = Enumerable.Range(0, 4).Select(_ => app.CreateBrowser()).ToList();
        var pages = await Task.WhenAll(browsers.Select(b => b.GetStringAsync("/account/register?tenant=acme", TestContext.Current.CancellationToken)));

        var results = await Task.WhenAll(browsers.Select((b, i) => b.PostAsync(
            "/account/register?tenant=acme",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Email"] = "shopper@mail.test",
                ["Password"] = Password,
                ["ConfirmPassword"] = Password,
                ["__RequestVerificationToken"] = CodeFlow.AntiforgeryToken(pages[i]),
            }),
            TestContext.Current.CancellationToken)));

        Assert.Single(results, r => r.StatusCode == HttpStatusCode.Redirect);
        foreach (var response in results.Where(r => r.StatusCode != HttpStatusCode.Redirect))
        {
            Assert.Contains(RegisterModel.AlreadyExists, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        }

        Assert.Equal(1, await CustomersAsync(app));
        browsers.ForEach(b => b.Dispose());
    }

    [Theory]
    [InlineData("/account/register?tenant=no-such-shop")]
    [InlineData("/account/register")]
    public async Task WithoutAKnownTenantThereIsNoForm(string url)
    {
        await using var app = await IdentityApp.StartAsync(containers);
        await app.WaitUntilMigratedAsync();
        using var browser = app.CreateBrowser();

        var page = await browser.GetStringAsync(url, TestContext.Current.CancellationToken);

        Assert.Contains(RegisterModel.NoShop, page, StringComparison.Ordinal);
        Assert.DoesNotContain("<form", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DirectRegistrationLandsOnTheSignedInPage()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        await app.CreateTenantAsync("acme");
        using var browser = app.CreateBrowser();

        var response = await CodeFlow.RegisterAsync(browser, "/account/register?tenant=acme&returnUrl=https%3A%2F%2Fevil.test%2F", "shopper@mail.test", Password);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/account/signed-in", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task PostWithoutAntiforgeryTokenIsRejected()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        await app.CreateTenantAsync("acme");
        using var browser = app.CreateBrowser();

        var response = await browser.PostAsync(
            "/account/register?tenant=acme",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["Email"] = "shopper@mail.test", ["Password"] = Password, ["ConfirmPassword"] = Password }),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await CustomersAsync(app));
    }

    private static Task<int> CustomersAsync(IdentityApp app) =>
        CreateTenantTests.CountAsync(app, db => db.Users.CountAsync(u => u.UserTypeId == UserTypes.CustomerValue.Id, TestContext.Current.CancellationToken));
}
