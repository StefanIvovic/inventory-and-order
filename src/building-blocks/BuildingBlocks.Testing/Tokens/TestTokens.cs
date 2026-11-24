using System.Security.Cryptography;
using BuildingBlocks.Core.Identity;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace BuildingBlocks.Testing;

public static class TestTokens
{
    public const string Issuer = "https://identity.test";
    public const string DefaultAudience = "test";

    private static readonly JsonWebTokenHandler Handler = new();
    private static readonly Lazy<RsaSecurityKey> SigningKey = new(() => CreateKey("test-key"));
    private static readonly Lazy<RsaSecurityKey> UnknownKey = new(() => CreateKey("unknown-key"));

    public static SecurityKey Key => SigningKey.Value;

    public static string For(Guid userId, string tenantId, UserType userType = UserType.Staff, string? audience = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        return Raw(new Dictionary<string, object>
        {
            [AppClaimTypes.Subject] = userId.ToString(),
            [AppClaimTypes.TenantId] = tenantId,
            [AppClaimTypes.UserType] = Name(userType),
            [JwtRegisteredClaimNames.Aud] = audience ?? DefaultAudience,
        });
    }

    public static string Platform(Guid userId, string? audience = null) =>
        Raw(new Dictionary<string, object>
        {
            [AppClaimTypes.Subject] = userId.ToString(),
            [AppClaimTypes.UserType] = Name(UserType.Platform),
            [JwtRegisteredClaimNames.Aud] = audience ?? DefaultAudience,
        });

    public static string Raw(IDictionary<string, object> claims, DateTimeOffset? expires = null, bool signWithUnknownKey = false) =>
        Raw(claims, signWithUnknownKey ? UnknownKey.Value : SigningKey.Value, expires);

    public static string Raw(IDictionary<string, object> claims, SecurityKey key, DateTimeOffset? expires = null)
    {
        ArgumentNullException.ThrowIfNull(claims);
        ArgumentNullException.ThrowIfNull(key);

        var expiry = (expires ?? DateTimeOffset.UtcNow.AddMinutes(15)).UtcDateTime;
        var values = new Dictionary<string, object>(claims);
        values.TryAdd(JwtRegisteredClaimNames.Aud, DefaultAudience);

        return Handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Claims = values,
            IssuedAt = expiry.AddMinutes(-20),
            NotBefore = expiry.AddMinutes(-20),
            Expires = expiry,
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256),
        });
    }

    public static RsaSecurityKey CreateKey(string keyId) => new(RSA.Create(2048)) { KeyId = keyId };

    private static string Name(UserType userType) => userType.ToString().ToLowerInvariant();
}
