using System.Text.Json;
using Gateway.Api.Configuration;
using Gateway.Api.Sessions;
using Gateway.Tests.Fakes;

namespace Gateway.Tests;

public sealed class BackchannelTests
{
    [Fact]
    public async Task PublicOriginRequestsGoToTheInternalAddressWithForwardedOrigin()
    {
        await using var identity = new SpaStub();
        await identity.StartAsync();
        using var client = ClientFor(new Uri("https://gateway.test/"), identity.Address);

        var body = await client.GetStringAsync(new Uri("https://gateway.test/connect/token?x=1"), TestContext.Current.CancellationToken);

        var echo = JsonSerializer.Deserialize<EchoBody>(body, JsonSerializerOptions.Web)!;
        Assert.Equal("/connect/token", echo.Path);
        Assert.Equal("?x=1", echo.Query);
        Assert.Equal("https", echo.Headers["X-Forwarded-Proto"]);
        Assert.Equal("gateway.test", echo.Headers["X-Forwarded-Host"]);
    }

    [Fact]
    public async Task OtherHostsAreNotTouched()
    {
        await using var other = new SpaStub();
        await other.StartAsync();
        using var client = ClientFor(new Uri("https://gateway.test/"), new Uri("http://127.0.0.1:1/"));

        var body = await client.GetStringAsync(new Uri(other.Address, "/x"), TestContext.Current.CancellationToken);

        var echo = JsonSerializer.Deserialize<EchoBody>(body, JsonSerializerOptions.Web)!;
        Assert.Equal("/x", echo.Path);
        Assert.False(echo.Headers.ContainsKey("X-Forwarded-Host"));
    }

    private static HttpClient ClientFor(Uri origin, Uri identity)
    {
        var settings = new GatewaySettings { PublicOrigin = origin, Identity = new IdentityEndpointSettings { InternalAddress = identity } };
#pragma warning disable CA2000
        return new HttpClient(new IdentityBackchannelHandler(settings) { InnerHandler = new HttpClientHandler() });
#pragma warning restore CA2000
    }
}
