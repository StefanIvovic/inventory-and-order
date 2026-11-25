using System.Net;
using System.Text.Json;
using Gateway.Api.Proxy;
using Gateway.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Yarp.ReverseProxy.Configuration;

namespace Gateway.Tests;

public sealed class RoutingTests
{
    private static readonly Dictionary<string, (string Path, string Cluster)> ReviewedRoutes = new(StringComparer.Ordinal)
    {
        ["identity-protocol"] = ("/connect/{**rest}", "identity"),
        ["identity-account"] = ("/account/{**rest}", "identity"),
        ["identity-discovery"] = ("/.well-known/{**rest}", "identity"),
        ["backoffice-spa"] = ("/backoffice/{**rest}", "backoffice-spa"),
        ["storefront-spa"] = ("/shop/{**rest}", "storefront-spa"),
    };

    [Fact]
    public async Task EveryApiRouteRequiresASessionAndEveryOtherRouteIsReviewed()
    {
        await using var app = await GatewayApp.StartAsync();
        var routes = app.Services.GetRequiredService<IProxyConfigProvider>().GetConfig().Routes;

        Assert.NotEmpty(routes);
        foreach (var route in routes)
        {
            var path = route.Match.Path!;
            if (path.StartsWith("/api/", StringComparison.Ordinal))
            {
                Assert.True(ApiSessionMiddleware.RequiresSession(route.Metadata), $"{route.RouteId} must require a session.");
                var prefix = path[..path.IndexOf("/{**rest}", StringComparison.Ordinal)];
                Assert.Contains(route.Transforms!, transform => transform.TryGetValue("PathRemovePrefix", out var value) && value == prefix);
            }
            else
            {
                Assert.True(ReviewedRoutes.TryGetValue(route.RouteId, out var reviewed), $"{route.RouteId} is not in the reviewed route list.");
                Assert.Equal(reviewed.Path, path);
                Assert.Equal(reviewed.Cluster, route.ClusterId);
                Assert.False(ApiSessionMiddleware.RequiresSession(route.Metadata));
            }
        }

        Assert.Contains(routes, route => route.RouteId == "api-identity");
    }

    [Theory]
    [InlineData("/backoffice/a/b?x=1", "backoffice")]
    [InlineData("/shop/acme/c", "storefront")]
    public async Task SpaPathsReachTheirUpstreamUnchangedWithoutGatewayCookies(string path, string spa)
    {
        await using var app = await GatewayApp.StartAsync();
        using var browser = await app.SignedInBackofficeAsync();

        var response = await browser.GetAsync(path, request => request.Headers.Add("Cookie", "theme=dark; .AspNetCore.Correlation.abc=x"));

        Assert.Equal(HttpStatusCode.OK, response.Status);
        var echo = JsonSerializer.Deserialize<EchoBody>(response.Body, JsonSerializerOptions.Web)!;
        Assert.Equal(path.Split('?')[0], echo.Path);
        Assert.Equal(spa == "backoffice" ? "?x=1" : string.Empty, echo.Query);
        Assert.Contains("theme=dark", echo.Headers["Cookie"], StringComparison.Ordinal);
        Assert.DoesNotContain("bff-backoffice", echo.Headers["Cookie"], StringComparison.Ordinal);
        Assert.DoesNotContain(".AspNetCore.", echo.Headers["Cookie"], StringComparison.Ordinal);
        Assert.Single((spa == "backoffice" ? app.BackofficeSpa : app.StorefrontSpa).RequestsTo(path.Split('?')[0]));
    }

    [Theory]
    [InlineData("/connect/x")]
    [InlineData("/account/css/identity.css")]
    [InlineData("/account/login?ReturnUrl=%2Fconnect%2Fauthorize")]
    public async Task IdentityPathsReachIdentityUnchanged(string path)
    {
        await using var app = await GatewayApp.StartAsync();
        using var browser = app.CreateBrowser();

        var response = await browser.GetAsync(path, request => request.Headers.Add("Cookie", "identity.session=abc"));

        Assert.Equal(HttpStatusCode.OK, response.Status);
        var echo = JsonSerializer.Deserialize<EchoBody>(response.Body, JsonSerializerOptions.Web)!;
        Assert.Equal(path, echo.Path + echo.Query);
        Assert.Equal("identity.session=abc", echo.Headers["Cookie"]);
    }

    [Fact]
    public async Task DiscoveryIsServedThroughTheGateway()
    {
        await using var app = await GatewayApp.StartAsync();
        using var browser = app.CreateBrowser();

        var response = await browser.GetAsync("/.well-known/openid-configuration");

        Assert.Equal(HttpStatusCode.OK, response.Status);
        using var json = JsonDocument.Parse(response.Body);
        Assert.Equal("https://gateway.test/", json.RootElement.GetProperty("issuer").GetString());
    }

    [Fact]
    public async Task ClientSentForwardedHeadersAreReplaced()
    {
        await using var app = await GatewayApp.StartAsync();
        using var browser = app.CreateBrowser();

        var response = await browser.GetAsync("/connect/x", request =>
        {
            request.Headers.Add("X-Forwarded-Host", "evil.test");
            request.Headers.Add("X-Forwarded-Proto", "gopher");
            request.Headers.Add("X-Forwarded-For", "6.6.6.6");
        });

        var echo = JsonSerializer.Deserialize<EchoBody>(response.Body, JsonSerializerOptions.Web)!;
        Assert.Equal("gateway.test", echo.Headers["X-Forwarded-Host"]);
        Assert.Equal("https", echo.Headers["X-Forwarded-Proto"]);
        Assert.DoesNotContain("6.6.6.6", echo.Headers.GetValueOrDefault("X-Forwarded-For") ?? string.Empty, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/anything")]
    [InlineData("/")]
    [InlineData("/css/identity.css")]
    [InlineData("/bff/unknown")]
    public async Task UnknownPathsAre404(string path)
    {
        await using var app = await GatewayApp.StartAsync();
        using var browser = app.CreateBrowser();

        var response = await browser.GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.Status);
        Assert.Empty(app.Identity.Requests);
    }

    [Fact]
    public async Task ReadyWithEveryUpstreamDown()
    {
        await using var app = await GatewayApp.StartAsync(overrides: new Dictionary<string, string?>
        {
            ["Gateway:Identity:InternalAddress"] = "http://127.0.0.1:9/",
            ["ReverseProxy:Clusters:identity:Destinations:main:Address"] = "http://127.0.0.1:9/",
            ["ReverseProxy:Clusters:backoffice-spa:Destinations:main:Address"] = "http://127.0.0.1:9/",
            ["ReverseProxy:Clusters:storefront-spa:Destinations:main:Address"] = "http://127.0.0.1:9/",
        });
        using var browser = app.CreateBrowser();

        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/health/ready")).Status);
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/health/live")).Status);
    }
}
