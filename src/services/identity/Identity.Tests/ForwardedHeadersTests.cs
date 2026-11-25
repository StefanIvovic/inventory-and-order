using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Tests;

public sealed class ForwardedHeadersTests(ContainersFixture containers)
{
    private const string GatewayIp = "172.18.0.5";
    private const string StrangerIp = "203.0.113.9";

    private static readonly Dictionary<string, string?> Trusted = new(StringComparer.Ordinal)
    {
        ["Identity:ForwardedHeaders:KnownNetworks:0"] = "172.16.0.0/12",
        ["Identity:ForwardedHeaders:AllowedHosts:0"] = "gateway.test",
    };

    [Fact]
    public async Task FromAKnownNetworkTheForwardedSchemeAndAllowedHostAreUsed()
    {
        await using var app = await StartAsync(Trusted);

        var seen = await SeenAsync(app, GatewayIp, "https", "gateway.test");

        Assert.Equal(("https", "gateway.test"), seen);
    }

    [Fact]
    public async Task AnIpv4MappedGatewayAddressIsTrusted()
    {
        await using var app = await StartAsync(Trusted);

        var seen = await SeenAsync(app, "::ffff:" + GatewayIp, "https", "gateway.test");

        Assert.Equal(("https", "gateway.test"), seen);
    }

    [Fact]
    public async Task AnAllowedHostWithAPortMatchesTheForwardedHostWithThatPort()
    {
        var settings = new Dictionary<string, string?>(Trusted) { ["Identity:ForwardedHeaders:AllowedHosts:0"] = "localhost:8080" };
        await using var app = await StartAsync(settings);

        var seen = await SeenAsync(app, GatewayIp, "https", "localhost:8080");

        Assert.Equal("https", seen.Scheme);
        Assert.Equal("8080", await SeenPortAsync(app, GatewayIp, "localhost:8080"));
    }

    [Fact]
    public async Task AForwardedHostThatIsNotAllowedIsIgnored()
    {
        await using var app = await StartAsync(Trusted);

        var seen = await SeenAsync(app, GatewayIp, "https", "evil.test");

        Assert.NotEqual("evil.test", seen.Host);
    }

    [Fact]
    public async Task FromAnUnknownAddressTheHeadersAreIgnored()
    {
        await using var app = await StartAsync(Trusted);

        var seen = await SeenAsync(app, StrangerIp, "https", "gateway.test");

        Assert.Equal(("http", "localhost"), seen);
    }

    [Fact]
    public async Task WithoutKnownNetworksNoForwardedHeaderIsTrusted()
    {
        await using var app = await StartAsync(new Dictionary<string, string?>());

        var seen = await SeenAsync(app, GatewayIp, "https", "gateway.test");

        Assert.Equal(("http", "localhost"), seen);
    }

    [Fact]
    public async Task ForwardedHttpsSatisfiesTheHttpsRequirementBehindTheGateway()
    {
        var settings = new Dictionary<string, string?>(Trusted) { ["Identity:RequireHttps"] = "true" };
        await using var app = await StartAsync(settings);

        var direct = await DiscoveryAsync(app, StrangerIp);
        var forwarded = await DiscoveryAsync(app, GatewayIp, "https");

        Assert.Equal(HttpStatusCode.BadRequest, direct);
        Assert.Equal(HttpStatusCode.OK, forwarded);
    }

    [Theory]
    [InlineData("Identity:ForwardedHeaders:AllowedHosts:0", null)]
    [InlineData("Identity:ForwardedHeaders:KnownNetworks:0", "not-a-network")]
    public async Task InvalidForwardedHeaderSettingsStopTheHost(string key, string? value)
    {
        var settings = new Dictionary<string, string?>(Trusted) { [key] = value };

        var error = await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await using var app = await StartAsync(settings);
        });

        Assert.Contains("Identity:ForwardedHeaders", error.ToString(), StringComparison.Ordinal);
    }

    private Task<IdentityApp> StartAsync(Dictionary<string, string?> settings) =>
        IdentityApp.StartAsync(containers, overrides: settings, testServices: services => services.AddTransient<IStartupFilter, RemoteAddressFilter>());

    private static async Task<(string Scheme, string Host)> SeenAsync(IdentityApp app, string remoteIp, string proto, string host)
    {
        using var client = app.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add(RemoteAddressFilter.Header, remoteIp);
        request.Headers.Add("X-Forwarded-Proto", proto);
        request.Headers.Add("X-Forwarded-Host", host);
        request.Headers.Add("X-Forwarded-For", "198.51.100.1");
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        return (response.Headers.GetValues(RemoteAddressFilter.SchemeHeader).Single(), response.Headers.GetValues(RemoteAddressFilter.HostHeader).Single());
    }

    private static async Task<string> SeenPortAsync(IdentityApp app, string remoteIp, string host)
    {
        using var client = app.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add(RemoteAddressFilter.Header, remoteIp);
        request.Headers.Add("X-Forwarded-Proto", "http");
        request.Headers.Add("X-Forwarded-Host", host);
        request.Headers.Add("X-Forwarded-For", "198.51.100.1");
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        return response.Headers.GetValues(RemoteAddressFilter.PortHeader).Single();
    }

    private static async Task<HttpStatusCode> DiscoveryAsync(IdentityApp app, string remoteIp, string? proto = null)
    {
        using var client = app.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/.well-known/openid-configuration");
        request.Headers.Add(RemoteAddressFilter.Header, remoteIp);
        if (proto is not null)
        {
            request.Headers.Add("X-Forwarded-Proto", proto);
            request.Headers.Add("X-Forwarded-Host", "gateway.test");
        }

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        return response.StatusCode;
    }

    private sealed class RemoteAddressFilter : IStartupFilter
    {
        public const string Header = "X-Test-Remote-Ip";
        public const string SchemeHeader = "X-Test-Seen-Scheme";
        public const string HostHeader = "X-Test-Seen-Host";
        public const string PortHeader = "X-Test-Seen-Port";

        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, inner) =>
            {
                if (IPAddress.TryParse(context.Request.Headers[Header].ToString(), out var address))
                {
                    context.Connection.RemoteIpAddress = address;
                }

                context.Response.OnStarting(() =>
                {
                    context.Response.Headers[SchemeHeader] = context.Request.Scheme;
                    context.Response.Headers[HostHeader] = context.Request.Host.Host;
                    context.Response.Headers[PortHeader] = context.Request.Host.Port?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
                    return Task.CompletedTask;
                });
                await inner(context);
            });
            next(app);
        };
    }
}
