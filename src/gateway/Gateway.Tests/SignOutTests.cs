using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace Gateway.Tests;

public sealed class SignOutTests
{
    [Fact]
    public async Task SignOutEndsTheSessionAtIdentityWithTheIdTokenHint()
    {
        await using var app = await GatewayApp.StartAsync();
        using var browser = await app.SignedInBackofficeAsync();
        var old = browser.Cookies.GetCookieHeader(browser.Origin);
        var logoutUrl = await LogoutUrlAsync(browser, "/bff/backoffice/user");
        Assert.Equal(1, app.Identity.ActiveRefreshTokens("backoffice-bff"));

        var landed = await browser.NavigateAsync(logoutUrl);

        Assert.Equal("/backoffice/", landed.Uri.AbsolutePath);
        Assert.Equal(0, app.Identity.ActiveRefreshTokens("backoffice-bff"));
        var endSession = Assert.Single(app.Identity.RequestsTo("/connect/endsession"));
        var query = QueryHelpers.ParseQuery(endSession.Query);
        Assert.False(string.IsNullOrEmpty(query["id_token_hint"]));
        Assert.Equal("https://gateway.test/bff/backoffice/signout-callback-oidc", query["post_logout_redirect_uri"]);
        Assert.DoesNotContain("__Host-Http-bff-backoffice", browser.CookieNames());

        using var client = app.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false, BaseAddress = app.Origin });
        using var request = new HttpRequestMessage(HttpMethod.Get, "/bff/backoffice/user");
        request.Headers.Add("Cookie", old);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task StorefrontSignOutLandsBackInTheShop()
    {
        await using var app = await GatewayApp.StartAsync();
        using var browser = await app.SignedInStorefrontAsync("acme");
        var logoutUrl = await LogoutUrlAsync(browser, "/bff/shop/acme/user");

        var landed = await browser.NavigateAsync(logoutUrl);

        Assert.Equal("/shop/acme/", landed.Uri.AbsolutePath);
        Assert.Equal(0, app.Identity.ActiveRefreshTokens("storefront-bff"));
        var endSession = Assert.Single(app.Identity.RequestsTo("/connect/endsession"));
        Assert.Equal("https://gateway.test/bff/storefront/signout-callback-oidc", QueryHelpers.ParseQuery(endSession.Query)["post_logout_redirect_uri"]);
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.GetAsync("/bff/shop/acme/user")).Status);
    }

    [Theory]
    [InlineData("/bff/backoffice/logout")]
    [InlineData("/bff/backoffice/logout?sid=")]
    [InlineData("/bff/backoffice/logout?sid=wrong")]
    public async Task MissingOrWrongProofIs400AndTheSessionStays(string path)
    {
        await using var app = await GatewayApp.StartAsync();
        using var browser = await app.SignedInBackofficeAsync();

        var response = await browser.GetAsync(path);

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.Contains("logout-proof-required", response.Body, StringComparison.Ordinal);
        Assert.Empty(app.Identity.RequestsTo("/connect/endsession"));
        Assert.Equal(1, app.Identity.ActiveRefreshTokens("backoffice-bff"));
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/bff/backoffice/user")).Status);
    }

    [Fact]
    public async Task SignOutWithoutSessionLandsOnTheStartPage()
    {
        await using var app = await GatewayApp.StartAsync();
        using var browser = app.CreateBrowser();

        var response = await browser.GetAsync("/bff/backoffice/logout?sid=anything");

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal("/backoffice/", response.Response.Headers.Location!.ToString());
    }

    private static async Task<string> LogoutUrlAsync(Fakes.Browser browser, string userPath)
    {
        var user = await browser.GetAsync(userPath);
        using var json = JsonDocument.Parse(user.Body);
        return json.RootElement.GetProperty("logoutUrl").GetString()!;
    }
}
