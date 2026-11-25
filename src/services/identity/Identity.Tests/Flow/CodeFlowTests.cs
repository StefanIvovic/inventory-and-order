using System.Net;

namespace Identity.Tests.Flow;

public sealed class CodeFlowTests(ContainersFixture containers)
{
    [Fact]
    public async Task FullFlowIssuesTokensAndTheyWorkOnTheApi()
    {
        await using var app = await IdentityApp.StartAsync(containers);

        var tokens = await CodeFlow.SignInAsync(app);

        Assert.NotNull(tokens.AccessToken);
        Assert.NotNull(tokens.RefreshToken);
        Assert.NotNull(tokens.IdToken);
        using var client = app.CreateClient();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/roles", tokens.AccessToken)).StatusCode);
    }

    [Fact]
    public async Task CodeCanBeRedeemedOnlyOnce()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        await app.WaitUntilMigratedAsync();
        using var browser = app.CreateBrowser();
        var (code, verifier) = await CodeFlow.AuthorizeAsync(browser, IdentityApp.AdminEmail, IdentityApp.AdminPassword);

        Assert.Equal(HttpStatusCode.OK, (await CodeFlow.RedeemAsync(browser, code, verifier)).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await CodeFlow.RedeemAsync(browser, code, verifier)).Status);
    }

    [Fact]
    public async Task ConcurrentCodeRedemptionSucceedsExactlyOnce()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        await app.WaitUntilMigratedAsync();
        using var browser = app.CreateBrowser();
        var (code, verifier) = await CodeFlow.AuthorizeAsync(browser, IdentityApp.AdminEmail, IdentityApp.AdminPassword);

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => CodeFlow.RedeemAsync(browser, code, verifier)));

        Assert.Single(results, r => r.Status == HttpStatusCode.OK);
        Assert.All(results.Where(r => r.Status != HttpStatusCode.OK), r => Assert.Equal("invalid_grant", r.Error));
    }

    [Fact]
    public async Task SecurityStampChangeEndsTheLoginSession()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        await app.WaitUntilMigratedAsync();
        using var browser = app.CreateBrowser();
        await CodeFlow.AuthorizeAsync(browser, IdentityApp.AdminEmail, IdentityApp.AdminPassword);
        await app.WithUsersAsync(async users =>
            await users.UpdateSecurityStampAsync((await users.FindByNameAsync(IdentityApp.AdminEmail))!));

        var again = await browser.GetAsync(CodeFlow.AuthorizeUrl(new AuthorizeRequest(), CodeFlow.NewPkce().Challenge), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, again.StatusCode);
        Assert.Contains("/account/login", again.Headers.Location!.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task WrongVerifierOrSecretIsRejected()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        await app.WaitUntilMigratedAsync();
        using var browser = app.CreateBrowser();
        var (code, _) = await CodeFlow.AuthorizeAsync(browser, IdentityApp.AdminEmail, IdentityApp.AdminPassword);
        var (other, verifier) = await CodeFlow.AuthorizeAsync(browser, IdentityApp.AdminEmail, IdentityApp.AdminPassword);

        var wrongVerifier = await CodeFlow.RedeemAsync(browser, code, CodeFlow.NewPkce().Verifier);
        var wrongSecret = await CodeFlow.RedeemAsync(browser, other, verifier, "not-the-secret");

        Assert.Equal(HttpStatusCode.BadRequest, wrongVerifier.Status);
        Assert.Null(wrongVerifier.AccessToken);
        Assert.Equal("invalid_grant", wrongVerifier.Error);
        Assert.Equal(HttpStatusCode.Unauthorized, wrongSecret.Status);
        Assert.Null(wrongSecret.AccessToken);
        Assert.Equal("invalid_client", wrongSecret.Error);
    }

    [Fact]
    public async Task UnregisteredRedirectUriIsRejectedWithoutRedirecting()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        await app.WaitUntilMigratedAsync();
        using var browser = app.CreateBrowser();
        var request = new AuthorizeRequest("openid identity", "https://evil.test/callback");

        var response = await browser.GetAsync(CodeFlow.AuthorizeUrl(request, CodeFlow.NewPkce().Challenge), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Theory]
    [InlineData(false, "openid identity", "invalid_request", HttpStatusCode.BadRequest)]
    [InlineData(true, "openid identity unknown-scope", "invalid_scope", HttpStatusCode.BadRequest)]
    [InlineData(true, "openid offline_access", "invalid_scope", HttpStatusCode.Redirect)]
    public async Task InvalidAuthorizationRequestIsRejectedWithoutACode(bool pkce, string scopes, string error, HttpStatusCode status)
    {
        await using var app = await IdentityApp.StartAsync(containers);
        await app.WaitUntilMigratedAsync();
        using var browser = app.CreateBrowser();
        var request = new AuthorizeRequest(scopes, IdentityApp.RedirectUri, pkce);

        var response = await browser.GetAsync(CodeFlow.AuthorizeUrl(request, CodeFlow.NewPkce().Challenge), TestContext.Current.CancellationToken);

        Assert.Equal(status, response.StatusCode);
        if (status == HttpStatusCode.Redirect)
        {
            var location = response.Headers.Location!;
            Assert.StartsWith(IdentityApp.RedirectUri, location.ToString(), StringComparison.Ordinal);
            Assert.Equal(error, CodeFlow.Query(location, "error"));
            Assert.Null(CodeFlow.Query(location, "code"));
        }
        else
        {
            Assert.Null(response.Headers.Location);
            Assert.Contains(error, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        }
    }
}
