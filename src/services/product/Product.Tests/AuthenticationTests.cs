using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BuildingBlocks.Core.Identity;
using BuildingBlocks.Testing;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Product.Tests;

public sealed class AuthenticationTests(ContainersFixture containers)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static Dictionary<string, object> Staff(string audience = ProductApp.Audience) => new()
    {
        [AppClaimTypes.Subject] = Guid.NewGuid().ToString(),
        [AppClaimTypes.TenantId] = "acme",
        [AppClaimTypes.UserType] = "staff",
        [JwtRegisteredClaimNames.Aud] = audience,
    };

    public static TheoryData<string, string?> Unauthenticated => new()
    {
        { "no token", null },
        { "expired", TestTokens.Raw(Staff(), DateTimeOffset.UtcNow.AddMinutes(-5)) },
        { "another audience", TestTokens.Raw(Staff("identity")) },
        { "unknown signing key", TestTokens.Raw(Staff(), signWithUnknownKey: true) },
    };

    [Theory]
    [MemberData(nameof(Unauthenticated))]
    public async Task InvalidTokensAre401(string reason, string? token)
    {
        await using var app = await MigratedAsync();

        var response = await GetAsync(app, token);

        Assert.True(response.StatusCode == HttpStatusCode.Unauthorized, reason);
    }

    [Fact]
    public async Task ATokenMalformedAsAnIdentityIs401InvalidIdentity()
    {
        await using var app = await MigratedAsync();
        var claims = Staff();
        claims.Remove(AppClaimTypes.TenantId);

        var response = await GetAsync(app, TestTokens.Raw(claims));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        Assert.Equal("invalid-identity", problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task ThePlatformAdminIs403OnTenantRoutes()
    {
        await using var app = await MigratedAsync();

        var response = await GetAsync(app, TestTokens.Platform(Guid.NewGuid(), ProductApp.Audience));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static async Task<HttpResponseMessage> GetAsync(ProductApp app, string? token)
    {
        using var client = app.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/roles");
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await client.SendAsync(request, Cancellation);
    }

    private async Task<ProductApp> MigratedAsync()
    {
        var app = await ProductApp.StartAsync(containers);
        await app.WaitUntilMigratedAsync();
        return app;
    }
}
