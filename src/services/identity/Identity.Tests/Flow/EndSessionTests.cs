using System.Net;
using System.Text.RegularExpressions;

namespace Identity.Tests.Flow;

public sealed partial class EndSessionTests(ContainersFixture containers)
{
    [Fact]
    public async Task SignOutConfirmsRedirectsAndRevokesRefreshTokens()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        await app.WaitUntilMigratedAsync();
        using var browser = app.CreateBrowser();
        var (code, verifier) = await CodeFlow.AuthorizeAsync(browser, IdentityApp.AdminEmail, IdentityApp.AdminPassword);
        var tokens = await CodeFlow.RedeemAsync(browser, code, verifier);
        var url = EndSessionUrl(IdentityApp.PostLogoutRedirectUri);

        var confirmation = await browser.GetAsync(url, TestContext.Current.CancellationToken);
        var page = await confirmation.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var posted = await browser.PostAsync(url, Form(page), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, confirmation.StatusCode);
        Assert.Contains("Sign out?", page, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Redirect, posted.StatusCode);
        Assert.StartsWith(IdentityApp.PostLogoutRedirectUri, posted.Headers.Location!.ToString(), StringComparison.Ordinal);
        Assert.Equal("invalid_grant", (await CodeFlow.RefreshAsync(browser, tokens.RefreshToken!)).Error);
    }

    [Fact]
    public async Task SignOutWithHintEndsOnlyThatSession()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        await app.WaitUntilMigratedAsync();
        using var first = app.CreateBrowser();
        using var second = app.CreateBrowser();
        var (firstCode, firstVerifier) = await CodeFlow.AuthorizeAsync(first, IdentityApp.AdminEmail, IdentityApp.AdminPassword);
        var signedOut = await CodeFlow.RedeemAsync(first, firstCode, firstVerifier);
        var (secondCode, secondVerifier) = await CodeFlow.AuthorizeAsync(second, IdentityApp.AdminEmail, IdentityApp.AdminPassword);
        var other = await CodeFlow.RedeemAsync(second, secondCode, secondVerifier);
        var url = EndSessionUrl(IdentityApp.PostLogoutRedirectUri, signedOut.IdToken);

        var page = await first.GetStringAsync(url, TestContext.Current.CancellationToken);
        var posted = await first.PostAsync(url, Form(page), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, posted.StatusCode);
        Assert.Equal("invalid_grant", (await CodeFlow.RefreshAsync(first, signedOut.RefreshToken!)).Error);
        Assert.Equal(HttpStatusCode.OK, (await CodeFlow.RefreshAsync(second, other.RefreshToken!)).Status);
    }

    [Fact]
    public async Task UnregisteredPostLogoutUriIsNeverRedirectedTo()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        await app.WaitUntilMigratedAsync();
        using var browser = app.CreateBrowser();
        await CodeFlow.AuthorizeAsync(browser, IdentityApp.AdminEmail, IdentityApp.AdminPassword);
        var url = EndSessionUrl("https://evil.test/after-logout");

        var confirmation = await browser.GetAsync(url, TestContext.Current.CancellationToken);
        var location = confirmation.Headers.Location?.ToString() ?? string.Empty;
        if (confirmation.StatusCode == HttpStatusCode.OK)
        {
            var posted = await browser.PostAsync(url, Form(await confirmation.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)), TestContext.Current.CancellationToken);
            location = posted.Headers.Location?.ToString() ?? string.Empty;
        }

        Assert.DoesNotContain("evil.test", location, StringComparison.Ordinal);
    }

    private static string EndSessionUrl(string postLogout, string? idTokenHint = null) =>
        $"/connect/endsession?client_id={IdentityApp.Client}&post_logout_redirect_uri={Uri.EscapeDataString(postLogout)}&state=bye"
        + (idTokenHint is null ? string.Empty : "&id_token_hint=" + Uri.EscapeDataString(idTokenHint));

    private static FormUrlEncodedContent Form(string page)
    {
        var fields = HiddenField().Matches(page).ToDictionary(m => WebUtility.HtmlDecode(m.Groups[1].Value), m => WebUtility.HtmlDecode(m.Groups[2].Value));
        fields["__RequestVerificationToken"] = CodeFlow.AntiforgeryToken(page);
        return new FormUrlEncodedContent(fields);
    }

    [GeneratedRegex("<input type=\"hidden\" name=\"([^\"]+)\" value=\"([^\"]*)\"")]
    private static partial Regex HiddenField();
}
