using System.Net;
using BuildingBlocks.Core.Identity;
using BuildingBlocks.Testing;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Identity.Tests;

public sealed class AuthenticationTests(ContainersFixture containers)
{
    private static readonly Guid UserId = Guid.Parse("5d1e8a20-7c3b-4f6a-9e02-8b4c1d7f3a65");

    public static TheoryData<string, string?> Unauthenticated => new()
    {
        { "none", null },
        { "unknown key", TestTokens.Raw(Claims("staff", "acme"), signWithUnknownKey: true) },
        { "expired", TestTokens.Raw(Claims("staff", "acme"), DateTimeOffset.UtcNow.AddMinutes(-10)) },
        { "wrong audience", TestTokens.For(UserId, "acme", UserType.Staff, "order") },
    };

    [Theory]
    [MemberData(nameof(Unauthenticated))]
    public async Task InvalidTokenIs401(string reason, string? token)
    {
        await using var app = await IdentityApp.StartAsync(containers);
        using var client = app.CreateClient();

        var response = await client.GetAsync("/roles", token);

        Assert.True(response.StatusCode == HttpStatusCode.Unauthorized, $"{reason}: {(int)response.StatusCode}");
    }

    [Theory]
    [InlineData("platform", "acme")]
    [InlineData("staff", null)]
    public async Task MalformedIdentityIs401InvalidIdentity(string userType, string? tenant)
    {
        await using var app = await IdentityApp.StartAsync(containers);
        using var client = app.CreateClient();

        var response = await client.GetAsync("/roles", TestTokens.Raw(Claims(userType, tenant)));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("invalid-identity", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    [Fact]
    public async Task PlatformUserOnTenantRouteIs403()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        using var client = app.CreateClient();

        var response = await client.GetAsync("/roles", TestTokens.Platform(UserId, IdentityApp.Audience));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task StaffWithoutRolesIs403()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        using var client = app.CreateClient();
        await app.WaitUntilMigratedAsync();

        var response = await client.GetAsync("/roles", TestTokens.For(UserId, "acme", UserType.Staff, IdentityApp.Audience));

        Assert.True(response.StatusCode == HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    private static Dictionary<string, object> Claims(string userType, string? tenant)
    {
        var claims = new Dictionary<string, object>
        {
            [AppClaimTypes.Subject] = UserId.ToString(),
            [AppClaimTypes.UserType] = userType,
            [JwtRegisteredClaimNames.Aud] = IdentityApp.Audience,
        };
        if (tenant is not null)
        {
            claims[AppClaimTypes.TenantId] = tenant;
        }

        return claims;
    }
}
