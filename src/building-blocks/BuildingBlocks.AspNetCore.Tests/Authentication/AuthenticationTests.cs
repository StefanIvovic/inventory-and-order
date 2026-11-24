using System.Net;
using BuildingBlocks.AspNetCore.Authentication;
using BuildingBlocks.Core.Identity;
using BuildingBlocks.Testing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.JsonWebTokens;

namespace BuildingBlocks.AspNetCore.Tests.Authentication;

public sealed class AuthenticationTests
{
    private static readonly Guid UserId = Guid.Parse("0b7f6c1e-3a52-4d8e-9a71-5c2f0e9d4b13");

    private static string Staff() => TestTokens.For(UserId, "acme", UserType.Staff, AuthApp.Audience);

    private static string Platform() => TestTokens.Platform(UserId, AuthApp.Audience);

    [Fact]
    public async Task MissingTokenIs401()
    {
        await using var app = await AuthApp.StartAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, (await app.GetAsync("/tenant-route")).StatusCode);
    }

    [Fact]
    public async Task ExpiredTokenIs401()
    {
        await using var app = await AuthApp.StartAsync();
        var token = TestTokens.Raw(StaffClaims(), DateTimeOffset.UtcNow.AddMinutes(-10));

        Assert.Equal(HttpStatusCode.Unauthorized, (await app.GetAsync("/tenant-route", token)).StatusCode);
    }

    [Fact]
    public async Task TokenSignedWithUnknownKeyIs401()
    {
        await using var app = await AuthApp.StartAsync();
        var token = TestTokens.Raw(StaffClaims(), signWithUnknownKey: true);

        Assert.Equal(HttpStatusCode.Unauthorized, (await app.GetAsync("/tenant-route", token)).StatusCode);
    }

    [Fact]
    public async Task TokenForAnotherAudienceIs401()
    {
        await using var app = await AuthApp.StartAsync();
        var token = TestTokens.For(UserId, "acme", UserType.Staff, "other-service");

        Assert.Equal(HttpStatusCode.Unauthorized, (await app.GetAsync("/tenant-route", token)).StatusCode);
    }

    [Theory]
    [InlineData("platform", "acme")]
    [InlineData("staff", null)]
    [InlineData("auditor", "acme")]
    public async Task MalformedIdentityIs401InvalidIdentity(string userType, string? tenant)
    {
        await using var app = await AuthApp.StartAsync();
        var claims = new Dictionary<string, object>
        {
            [AppClaimTypes.Subject] = UserId.ToString(),
            [AppClaimTypes.UserType] = userType,
            [JwtRegisteredClaimNames.Aud] = AuthApp.Audience,
        };
        if (tenant is not null)
        {
            claims[AppClaimTypes.TenantId] = tenant;
        }

        foreach (var path in new[] { "/tenant-route", "/platform-route" })
        {
            var response = await app.GetAsync(path, TestTokens.Raw(claims));
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Contains("invalid-identity", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task PlatformUserOnTenantRouteIs403()
    {
        await using var app = await AuthApp.StartAsync();

        Assert.Equal(HttpStatusCode.Forbidden, (await app.GetAsync("/tenant-route", Platform())).StatusCode);
    }

    [Theory]
    [InlineData("/platform-route")]
    [InlineData("/platform-attribute")]
    public async Task TenantUserOnPlatformRouteIs403(string path)
    {
        await using var app = await AuthApp.StartAsync();

        Assert.Equal(HttpStatusCode.Forbidden, (await app.GetAsync(path, Staff())).StatusCode);
    }

    [Theory]
    [InlineData("/tenant-route", "staff")]
    [InlineData("/tenant-route", "customer")]
    [InlineData("/platform-route", "platform")]
    [InlineData("/platform-attribute", "platform")]
    [InlineData("/open", null)]
    public async Task RightCallerIsAllowed(string path, string? caller)
    {
        await using var app = await AuthApp.StartAsync();
        var token = caller switch
        {
            "staff" => Staff(),
            "customer" => TestTokens.For(UserId, "acme", UserType.Customer, AuthApp.Audience),
            "platform" => Platform(),
            _ => null,
        };

        Assert.Equal(HttpStatusCode.OK, (await app.GetAsync(path, token)).StatusCode);
    }

    [Fact]
    public async Task UnmarkedRouteRequiresTenantByDefault()
    {
        await using var app = await AuthApp.StartAsync(routes: a => a.MapGet("/new-route", () => "new"));

        Assert.Equal(HttpStatusCode.Unauthorized, (await app.GetAsync("/new-route")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await app.GetAsync("/new-route", Platform())).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await app.GetAsync("/new-route", Staff())).StatusCode);
    }

    [Fact]
    public async Task BothMarkersFailStartup()
    {
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            AuthApp.StartAsync(routes: a => a.MapGet("/confused", () => "x").AllowAnonymous().RequirePlatform()));

        Assert.Contains("/confused", error.Message, StringComparison.Ordinal);
    }

    private static Dictionary<string, object> StaffClaims() => new()
    {
        [AppClaimTypes.Subject] = UserId.ToString(),
        [AppClaimTypes.TenantId] = "acme",
        [AppClaimTypes.UserType] = "staff",
        [JwtRegisteredClaimNames.Aud] = AuthApp.Audience,
    };
}
