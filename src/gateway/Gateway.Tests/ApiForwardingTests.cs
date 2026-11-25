using System.Net;
using System.Text.Json;
using Gateway.Tests.Fakes;

namespace Gateway.Tests;

public sealed class ApiForwardingTests
{
    [Fact]
    public async Task SignedInCallReachesTheServiceWithTheBearerTokenAndNoBrowserCredentials()
    {
        await using var app = await GatewayApp.StartAsync();
        using var browser = await app.SignedInBackofficeAsync();

        var response = await browser.GetAsync("/api/identity/users?x=1", request =>
        {
            request.Headers.Add("X-Bff-App", "backoffice");
            request.Headers.Add("Authorization", "Bearer browser-made");
            request.Headers.Add("traceparent", "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01");
        });

        Assert.Equal(HttpStatusCode.OK, response.Status);
        var echo = JsonSerializer.Deserialize<EchoBody>(response.Body, JsonSerializerOptions.Web)!;
        Assert.Equal("/users", echo.Path);
        Assert.Equal("?x=1", echo.Query);
        Assert.Equal("Bearer " + app.Identity.IssuedAccessTokens.Single(), echo.Headers["Authorization"]);
        Assert.False(echo.Headers.ContainsKey("Cookie"));
        Assert.False(echo.Headers.ContainsKey("X-Bff-App"));
        Assert.Contains("0af7651916cd43dd8448eb211c80319c", echo.Headers["traceparent"], StringComparison.Ordinal);
        Assert.Equal("https", echo.Headers["X-Forwarded-Proto"]);
        Assert.Equal("gateway.test", echo.Headers["X-Forwarded-Host"]);
    }

    [Fact]
    public async Task NoSessionIs401AndNothingIsForwarded()
    {
        await using var app = await GatewayApp.StartAsync();
        using var browser = app.CreateBrowser();

        var response = await browser.GetAsync("/api/identity/users", request => request.Headers.Add("X-Bff-App", "backoffice"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.Status);
        Assert.Contains("session-required", response.Body, StringComparison.Ordinal);
        Assert.Empty(app.Identity.RequestsTo("/users"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("admin")]
    [InlineData("Backoffice")]
    public async Task MissingOrUnknownAppHeaderIs400(string? header)
    {
        await using var app = await GatewayApp.StartAsync();
        using var browser = await app.SignedInBackofficeAsync();

        var response = await browser.GetAsync("/api/identity/users", request =>
        {
            if (header is not null)
            {
                request.Headers.Add("X-Bff-App", header);
            }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.Contains("app-header-required", response.Body, StringComparison.Ordinal);
        Assert.Empty(app.Identity.RequestsTo("/users"));
    }

    [Theory]
    [InlineData("cross-site")]
    [InlineData("same-site")]
    [InlineData("none")]
    public async Task CrossSiteFetchIs403(string site)
    {
        await using var app = await GatewayApp.StartAsync();
        using var browser = await app.SignedInBackofficeAsync();

        var response = await browser.GetAsync("/api/identity/users", request =>
        {
            request.Headers.Add("X-Bff-App", "backoffice");
            request.Headers.Add("Sec-Fetch-Site", site);
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.Status);
        Assert.Contains("cross-site-request", response.Body, StringComparison.Ordinal);
        Assert.Empty(app.Identity.RequestsTo("/users"));
    }

    [Fact]
    public async Task SameOriginFetchIsForwarded()
    {
        await using var app = await GatewayApp.StartAsync();
        using var browser = await app.SignedInBackofficeAsync();

        var response = await browser.GetAsync("/api/identity/users", request =>
        {
            request.Headers.Add("X-Bff-App", "backoffice");
            request.Headers.Add("Sec-Fetch-Site", "same-origin");
        });

        Assert.Equal(HttpStatusCode.OK, response.Status);
    }

    [Theory]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(409)]
    [InlineData(422)]
    public async Task ServiceAnswersPassThroughUnchanged(int status)
    {
        await using var app = await GatewayApp.StartAsync();
        using var browser = await app.SignedInBackofficeAsync();

        var response = await browser.GetAsync($"/api/identity/status/{status}", request => request.Headers.Add("X-Bff-App", "backoffice"));

        Assert.Equal(status, (int)response.Status);
        Assert.Contains($"upstream-{status}", response.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnknownServiceIs404()
    {
        await using var app = await GatewayApp.StartAsync();
        using var browser = await app.SignedInBackofficeAsync();

        var response = await browser.GetAsync("/api/unknown/x", request => request.Headers.Add("X-Bff-App", "backoffice"));

        Assert.Equal(HttpStatusCode.NotFound, response.Status);
    }

    [Fact]
    public async Task AppHeaderSelectsWhichSessionIsUsed()
    {
        await using var app = await GatewayApp.StartAsync();
        using var browser = await app.SignedInBackofficeAsync();
        var backofficeToken = app.Identity.IssuedAccessTokens.Single();
        await app.SignedInStorefrontAsync("acme", browser);
        var storefrontToken = app.Identity.IssuedAccessTokens.Last();

        var storefront = await browser.GetAsync("/api/identity/x", request => request.Headers.Add("X-Bff-App", "storefront"));
        var backoffice = await browser.GetAsync("/api/identity/x", request => request.Headers.Add("X-Bff-App", "backoffice"));

        Assert.Equal("Bearer " + storefrontToken, JsonSerializer.Deserialize<EchoBody>(storefront.Body, JsonSerializerOptions.Web)!.Headers["Authorization"]);
        Assert.Equal("Bearer " + backofficeToken, JsonSerializer.Deserialize<EchoBody>(backoffice.Body, JsonSerializerOptions.Web)!.Headers["Authorization"]);
    }

    [Fact]
    public async Task StorefrontHeaderWithoutStorefrontSessionIs401()
    {
        await using var app = await GatewayApp.StartAsync();
        using var browser = await app.SignedInBackofficeAsync();

        var response = await browser.GetAsync("/api/identity/x", request => request.Headers.Add("X-Bff-App", "storefront"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.Status);
    }

    [Fact]
    public async Task UnreachableUpstreamIs502Problem()
    {
        await using var app = await GatewayApp.StartAsync(overrides: new Dictionary<string, string?>
        {
            ["ReverseProxy:Clusters:backoffice-spa:Destinations:main:Address"] = "http://127.0.0.1:9/",
        });
        using var browser = app.CreateBrowser();

        var response = await browser.GetAsync("/backoffice/");

        Assert.Equal(HttpStatusCode.BadGateway, response.Status);
        Assert.Contains("upstream-unavailable", response.Body, StringComparison.Ordinal);
    }
}
