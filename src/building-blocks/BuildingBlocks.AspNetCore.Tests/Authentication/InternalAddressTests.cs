using System.Net;
using BuildingBlocks.AspNetCore.Authentication;
using BuildingBlocks.Core.Identity;
using BuildingBlocks.Testing;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;

namespace BuildingBlocks.AspNetCore.Tests.Authentication;

public sealed class InternalAddressTests
{
    private static readonly Dictionary<string, object> StaffClaims = new()
    {
        [AppClaimTypes.Subject] = Guid.NewGuid().ToString(),
        [AppClaimTypes.TenantId] = "acme",
        [AppClaimTypes.UserType] = "staff",
        [JwtRegisteredClaimNames.Aud] = AuthApp.Audience,
    };

    [Fact]
    public async Task TokensValidateWhenTheIssuerIsReachableOnlyAtTheInternalAddress()
    {
        await using var authority = await StubAuthority.StartAsync();
        var key = TestTokens.CreateKey("internal");
        authority.Publish(key);
        await using var app = await AuthApp.StartAsync(authority: TestTokens.Issuer, trustTestKey: false, internalAddress: authority.Address);

        var response = await app.GetAsync("/tenant-route", TestTokens.Raw(StaffClaims, key));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEmpty(authority.Forwarded);
        Assert.All(authority.Forwarded, headers => Assert.Equal(("https", "identity.test"), headers));
    }

    [Fact]
    public async Task WithoutTheInternalAddressTheUnreachableIssuerGives401()
    {
        await using var authority = await StubAuthority.StartAsync();
        var key = TestTokens.CreateKey("internal");
        authority.Publish(key);
        await using var app = await AuthApp.StartAsync(authority: TestTokens.Issuer, trustTestKey: false);

        var response = await app.GetAsync("/tenant-route", TestTokens.Raw(StaffClaims, key));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(authority.Forwarded);
    }

    [Fact]
    public async Task RotatedKeysAreFetchedThroughTheInternalAddress()
    {
        await using var authority = await StubAuthority.StartAsync();
        var first = TestTokens.CreateKey("first");
        var second = TestTokens.CreateKey("second");
        authority.Publish(first);
        await using var app = await AuthApp.StartAsync(
            authority: TestTokens.Issuer,
            trustTestKey: false,
            services: s => s.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, o => o.RefreshInterval = TimeSpan.FromSeconds(1)),
            internalAddress: authority.Address);

        Assert.Equal(HttpStatusCode.OK, (await app.GetAsync("/tenant-route", TestTokens.Raw(StaffClaims, first))).StatusCode);

        authority.Publish(second);
        var deadline = DateTimeOffset.UtcNow.AddSeconds(15);
        var status = HttpStatusCode.Unauthorized;
        while (status != HttpStatusCode.OK && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(500), TestContext.Current.CancellationToken);
            status = (await app.GetAsync("/tenant-route", TestTokens.Raw(StaffClaims, second))).StatusCode;
        }

        Assert.Equal(HttpStatusCode.OK, status);
    }

    [Theory]
    [InlineData("identity-api:8080")]
    [InlineData("/internal")]
    [InlineData("http://identity-api:8080/path")]
    [InlineData("ftp://identity-api")]
    public async Task AnInternalAddressThatIsNotAnOriginFailsAtStart(string internalAddress)
    {
        var error = await Assert.ThrowsAnyAsync<Exception>(() => AuthApp.StartAsync(internalAddress: internalAddress));

        Assert.Contains("Authentication:InternalAddress", error.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("https://identity.test/.well-known/openid-configuration", "http://10.0.0.5:8080/.well-known/openid-configuration", true)]
    [InlineData("https://identity.test/jwks?x=1", "http://10.0.0.5:8080/jwks?x=1", true)]
    [InlineData("https://other.test/jwks", "https://other.test/jwks", false)]
    [InlineData("http://identity.test/jwks", "http://identity.test/jwks", false)]
    [InlineData("https://identity.test:8443/jwks", "https://identity.test:8443/jwks", false)]
    public async Task OnlyRequestsToTheAuthorityOriginAreRewritten(string requested, string sent, bool forwarded)
    {
        var inner = new RecordingHandler();
        using var handler = new IssuerBackchannelHandler(new Uri("https://identity.test/"), new Uri("http://10.0.0.5:8080")) { InnerHandler = inner };
        using var client = new HttpClient(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, requested);
        request.Headers.TryAddWithoutValidation("X-Forwarded-Host", "spoofed");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(sent, inner.Uri!.ToString());
        if (forwarded)
        {
            Assert.Equal(["https"], inner.Headers!.GetValues("X-Forwarded-Proto"));
            Assert.Equal(["identity.test"], inner.Headers!.GetValues("X-Forwarded-Host"));
        }
        else
        {
            Assert.Equal(["spoofed"], inner.Headers!.GetValues("X-Forwarded-Host"));
        }
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public Uri? Uri { get; private set; }

        public System.Net.Http.Headers.HttpRequestHeaders? Headers { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Uri = request.RequestUri;
            Headers = request.Headers;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
