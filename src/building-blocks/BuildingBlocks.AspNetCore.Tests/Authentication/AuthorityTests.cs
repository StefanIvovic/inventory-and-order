using System.Net;
using BuildingBlocks.Core.Identity;
using BuildingBlocks.Testing;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;

namespace BuildingBlocks.AspNetCore.Tests.Authentication;

public sealed class AuthorityTests
{
    private static readonly Dictionary<string, object> StaffClaims = new()
    {
        [AppClaimTypes.Subject] = Guid.NewGuid().ToString(),
        [AppClaimTypes.TenantId] = "acme",
        [AppClaimTypes.UserType] = "staff",
        [JwtRegisteredClaimNames.Aud] = AuthApp.Audience,
    };

    [Fact]
    public async Task UnreachableAuthorityKeepsServiceLiveAndAnswers401()
    {
        await using var app = await AuthApp.StartAsync(authority: "http://127.0.0.1:9", trustTestKey: false);

        Assert.Equal(HttpStatusCode.OK, (await app.GetAsync("/open")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await app.GetAsync("/tenant-route", TestTokens.Raw(StaffClaims))).StatusCode);
    }

    [Fact]
    public async Task RotatedSigningKeyIsAcceptedWithoutRestart()
    {
        await using var authority = await StubAuthority.StartAsync();
        var first = TestTokens.CreateKey("first");
        var second = TestTokens.CreateKey("second");
        authority.Publish(first);
        await using var app = await AuthApp.StartAsync(
            authority: authority.Address,
            trustTestKey: false,
            services: s => s.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, o => o.RefreshInterval = TimeSpan.FromSeconds(1)));

        Assert.Equal(HttpStatusCode.OK, (await app.GetAsync("/tenant-route", TestTokens.Raw(StaffClaims, first))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await app.GetAsync("/tenant-route", TestTokens.Raw(StaffClaims, second))).StatusCode);

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
}
