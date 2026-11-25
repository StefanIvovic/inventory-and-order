using System.Net;
using System.Text.Json;
using Gateway.Api.Sessions;
using Gateway.Tests.Fakes;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Gateway.Tests;

public sealed class SignInTests
{
    [Fact]
    public async Task LoginRedirectsToIdentityAsTheBackofficeClientWithPkce()
    {
        await using var app = await GatewayApp.StartAsync();
        using var browser = app.CreateBrowser();

        var response = await browser.GetAsync("/bff/backoffice/login");

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        var location = response.Response.Headers.Location!;
        Assert.Equal("/connect/authorize", location.AbsolutePath);
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(location.Query);
        Assert.Equal("backoffice-bff", query["client_id"]);
        Assert.Equal("code", query["response_type"]);
        Assert.Equal("form_post", query["response_mode"]);
        Assert.Equal("S256", query["code_challenge_method"]);
        Assert.Equal(string.Join(' ', SessionSetup.Scopes), query["scope"]);
        Assert.False(query.ContainsKey("tenant"));
    }

    [Fact]
    public async Task SignInSetsOnlyTheSessionCookieAndTheUserEndpointShowsTheUser()
    {
        await using var app = await GatewayApp.StartAsync();
        using var browser = await app.SignedInBackofficeAsync();

        var session = Assert.Single(browser.SetCookieHeaders, header => header.StartsWith("__Host-Http-bff-backoffice=", StringComparison.Ordinal));
        Assert.Contains("httponly", session, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", session, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", session, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", session, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("expires=", session, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("domain=", session, StringComparison.OrdinalIgnoreCase);
        Assert.All(
            browser.SetCookieHeaders.Where(header => header.StartsWith(".AspNetCore.", StringComparison.Ordinal)),
            header => Assert.Contains("samesite=lax", header, StringComparison.OrdinalIgnoreCase));
        Assert.Equal(["__Host-Http-bff-backoffice"], browser.CookieNames());

        var user = await browser.GetAsync("/bff/backoffice/user");

        Assert.Equal(HttpStatusCode.OK, user.Status);
        using var json = JsonDocument.Parse(user.Body);
        Assert.Equal("user-1", json.RootElement.GetProperty("userId").GetString());
        Assert.Equal("staff", json.RootElement.GetProperty("userType").GetString());
        Assert.Equal("tenant-1", json.RootElement.GetProperty("tenantId").GetString());
        Assert.Equal("staff@acme.test", json.RootElement.GetProperty("email").GetString());
        Assert.StartsWith("/bff/backoffice/logout?sid=", json.RootElement.GetProperty("logoutUrl").GetString(), StringComparison.Ordinal);
        Assert.False(json.RootElement.TryGetProperty("slug", out _));
        Assertions.NoTokens(app, browser);
    }

    [Fact]
    public async Task PlatformAdminHasNoTenant()
    {
        await using var app = await GatewayApp.StartAsync();
        app.Identity.User = new FakeUser("admin-1", "platform", null, "admin@platform.test");
        using var browser = await app.SignedInBackofficeAsync();

        var user = await browser.GetAsync("/bff/backoffice/user");

        using var json = JsonDocument.Parse(user.Body);
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("tenantId").ValueKind);
    }

    [Fact]
    public async Task UserEndpointWithoutSessionIs401WithoutRedirect()
    {
        await using var app = await GatewayApp.StartAsync();
        using var browser = app.CreateBrowser();

        var response = await browser.GetAsync("/bff/backoffice/user");

        Assert.Equal(HttpStatusCode.Unauthorized, response.Status);
        Assert.Equal("application/problem+json", response.Response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("session-required", response.Body, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/backoffice/orders?x=1", "/backoffice/orders")]
    [InlineData("https://evil.test/backoffice/", "/backoffice/")]
    [InlineData("//evil.test/backoffice/", "/backoffice/")]
    [InlineData("/shop/acme/", "/backoffice/")]
    [InlineData("/backoffice/../bff/backoffice/user", "/backoffice/")]
    public async Task ReturnPathIsFollowedOnlyInsideTheBackoffice(string returnUrl, string landing)
    {
        await using var app = await GatewayApp.StartAsync();
        using var browser = app.CreateBrowser();

        var landed = await browser.NavigateAsync("/bff/backoffice/login?returnUrl=" + Uri.EscapeDataString(returnUrl));

        Assert.Equal(new Uri(app.Origin, landing).AbsolutePath, landed.Uri.AbsolutePath);
        Assert.Equal(app.Origin.Host, landed.Uri.Host);
    }

    [Fact]
    public async Task RefusedSignInLandsOnTheStartPageWithAnErrorAndNoSession()
    {
        await using var app = await GatewayApp.StartAsync();
        app.Identity.DenySignIn = true;
        using var browser = app.CreateBrowser();

        var landed = await browser.NavigateAsync("/bff/backoffice/login");

        Assert.Equal("/backoffice/", landed.Uri.AbsolutePath);
        Assert.Equal("?signin-error=access_denied", landed.Uri.Query);
        Assert.DoesNotContain("__Host-Http-bff-backoffice", browser.CookieNames());
    }

    [Fact]
    public async Task ReplayedCallbackCreatesNoSession()
    {
        await using var app = await GatewayApp.StartAsync();
        using var attacker = app.CreateBrowser();
        var replay = await attacker.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/bff/backoffice/signin-oidc")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["code"] = "stolen", ["state"] = "forged" }),
        });

        Assert.Equal(HttpStatusCode.Redirect, replay.Status);
        Assert.Contains("signin-error=", replay.Response.Headers.Location!.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("__Host-Http-bff-backoffice", attacker.CookieNames());
        var user = await attacker.GetAsync("/bff/backoffice/user");
        Assert.Equal(HttpStatusCode.Unauthorized, user.Status);
    }

    [Fact]
    public async Task TamperedCookieCountsAsNoSession()
    {
        await using var app = await GatewayApp.StartAsync();
        using var browser = app.CreateBrowser();

        var response = await browser.GetAsync("/bff/backoffice/user", request => request.Headers.Add("Cookie", "__Host-Http-bff-backoffice=CfDJ8tampered"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.Status);
    }

    [Fact]
    public async Task PlainHttpOriginUsesUnprefixedCookieAndAllowsHttpMetadata()
    {
        await using var app = await GatewayApp.StartAsync("http://gateway.test");
        using var browser = app.CreateBrowser();

        var response = await browser.GetAsync("/bff/backoffice/login");

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal("http://gateway.test/connect/authorize", response.Response.Headers.Location!.GetLeftPart(UriPartial.Path));
        var cookie = app.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(BffApp.Backoffice.CookieScheme);
        Assert.Equal("bff-backoffice", cookie.Cookie.Name);
    }
}
