using System.Diagnostics;
using System.Net;

namespace Identity.Tests.Flow;

public sealed class CodeFlowTests(ContainersFixture containers)
{
    [Fact]
    public async Task FullFlowIssuesTokensQuicklyAndTheyWorkOnTheApi()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        await app.WaitUntilMigratedAsync();
        var timer = Stopwatch.StartNew();

        var tokens = await CodeFlow.SignInAsync(app);

        Assert.True(timer.Elapsed < TimeSpan.FromSeconds(5), $"The code flow took {timer.Elapsed}.");
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
    public async Task WrongVerifierOrSecretIsRejected()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        await app.WaitUntilMigratedAsync();
        using var browser = app.CreateBrowser();
        var (code, _) = await CodeFlow.AuthorizeAsync(browser, IdentityApp.AdminEmail, IdentityApp.AdminPassword);
        var (other, verifier) = await CodeFlow.AuthorizeAsync(browser, IdentityApp.AdminEmail, IdentityApp.AdminPassword);

        var wrongVerifier = await CodeFlow.RedeemAsync(browser, code, CodeFlow.NewPkce().Verifier);
        var wrongSecret = await CodeFlow.RedeemAsync(browser, other, verifier, "not-the-secret");

        Assert.Null(wrongVerifier.AccessToken);
        Assert.Equal("invalid_grant", wrongVerifier.Error);
        Assert.Null(wrongSecret.AccessToken);
        Assert.Equal("invalid_client", wrongSecret.Error);
    }

    [Theory]
    [InlineData("https://evil.test/callback", true, "openid identity")]
    [InlineData(IdentityApp.RedirectUri, false, "openid identity")]
    [InlineData(IdentityApp.RedirectUri, true, "openid offline_access")]
    [InlineData(IdentityApp.RedirectUri, true, "openid identity unknown-scope")]
    public async Task InvalidAuthorizationRequestIsRejectedWithoutRedirectingElsewhere(string redirectUri, bool pkce, string scopes)
    {
        await using var app = await IdentityApp.StartAsync(containers);
        await app.WaitUntilMigratedAsync();
        using var browser = app.CreateBrowser();
        var request = new AuthorizeRequest(scopes, redirectUri, pkce);

        var response = await browser.GetAsync(CodeFlow.AuthorizeUrl(request, CodeFlow.NewPkce().Challenge), TestContext.Current.CancellationToken);

        var location = response.Headers.Location?.ToString() ?? string.Empty;
        Assert.DoesNotContain("evil.test", location, StringComparison.Ordinal);
        Assert.DoesNotContain("/account/login", location, StringComparison.Ordinal);
        Assert.DoesNotContain("code=", location, StringComparison.Ordinal);
    }
}
