using System.Net;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Identity.Tests.Flow;

public sealed class TokenTests(ContainersFixture containers)
{
    private static readonly string[] Forbidden = ["role", "roles", "permission", "permissions", "email", "name", "identity_stamp"];

    [Fact]
    public async Task AccessTokenCarriesOnlyIdentityForTheRequestedServices()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        var tokens = await CodeFlow.SignInAsync(app, request: new AuthorizeRequest("openid offline_access identity order"));

        var jwt = new JsonWebToken(tokens.AccessToken);

        Assert.Equal("at+jwt", jwt.Typ);
        Assert.False(jwt.IsEncrypted);
        Assert.Equal("platform", jwt.GetClaim("user_type").Value);
        Assert.False(jwt.TryGetClaim("tenant_id", out _));
        Assert.True(Guid.TryParse(jwt.Subject, out _));
        Assert.All(Forbidden, claim => Assert.False(jwt.TryGetClaim(claim, out _), $"unexpected claim {claim}"));
        Assert.Equal(900, (jwt.ValidTo - jwt.IssuedAt).TotalSeconds);
        Assert.Equal(["identity", "order"], jwt.Audiences.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task TokenWithoutIdentityAudienceIsRejectedByIdentity()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        var tokens = await CodeFlow.SignInAsync(app, request: new AuthorizeRequest("openid order"));
        using var client = app.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/roles", tokens.AccessToken)).StatusCode);
    }

    [Fact]
    public async Task SignatureVerifiesAgainstPublishedKeys()
    {
        await using var app = await IdentityApp.StartAsync(containers, testSigningKey: false);
        var tokens = await CodeFlow.SignInAsync(app);
        using var client = app.CreateClient();
        var jwks = new JsonWebKeySet(await client.GetStringAsync("/.well-known/jwks", TestContext.Current.CancellationToken));

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(tokens.AccessToken, new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            IssuerSigningKeys = jwks.GetSigningKeys(),
        });

        Assert.True(result.IsValid, result.Exception?.Message);
    }

    [Fact]
    public async Task DiscoveryAdvertisesOnlyTheCodeFlowWithS256()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        await app.WaitUntilMigratedAsync();
        using var client = app.CreateClient();

        using var discovery = JsonDocument.Parse(await client.GetStringAsync("/.well-known/openid-configuration", TestContext.Current.CancellationToken));
        var root = discovery.RootElement;

        Assert.Equal(["code"], root.GetProperty("response_types_supported").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal(["S256"], root.GetProperty("code_challenge_methods_supported").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal(
            ["authorization_code", "refresh_token"],
            root.GetProperty("grant_types_supported").EnumerateArray().Select(e => e.GetString()).Order(StringComparer.Ordinal));
    }
}
