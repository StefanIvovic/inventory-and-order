using System.Globalization;
using Microsoft.AspNetCore.Authentication;

namespace Gateway.Api.Sessions;

public sealed record SessionTokens(string AccessToken, string RefreshToken, string? IdToken, DateTimeOffset? ExpiresAt)
{
    public const string AccessTokenName = "access_token";
    public const string RefreshTokenName = "refresh_token";
    public const string IdTokenName = "id_token";
    public const string ExpiresAtName = "expires_at";

    public static SessionTokens? From(AuthenticationProperties properties)
    {
        ArgumentNullException.ThrowIfNull(properties);

        var access = properties.GetTokenValue(AccessTokenName);
        var refresh = properties.GetTokenValue(RefreshTokenName);
        if (string.IsNullOrEmpty(access) || string.IsNullOrEmpty(refresh))
        {
            return null;
        }

        var expires = DateTimeOffset.TryParse(properties.GetTokenValue(ExpiresAtName), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var value)
            ? value
            : (DateTimeOffset?)null;
        return new SessionTokens(access, refresh, properties.GetTokenValue(IdTokenName), expires);
    }

    public void WriteTo(AuthenticationProperties properties)
    {
        ArgumentNullException.ThrowIfNull(properties);

        var tokens = new List<AuthenticationToken>
        {
            new() { Name = AccessTokenName, Value = AccessToken },
            new() { Name = RefreshTokenName, Value = RefreshToken },
        };
        if (IdToken is not null)
        {
            tokens.Add(new AuthenticationToken { Name = IdTokenName, Value = IdToken });
        }

        if (ExpiresAt is { } expires)
        {
            tokens.Add(new AuthenticationToken { Name = ExpiresAtName, Value = expires.ToString("o", CultureInfo.InvariantCulture) });
        }

        properties.StoreTokens(tokens);
    }

    public bool NeedsRefresh(DateTimeOffset now, TimeSpan threshold) => ExpiresAt is not { } expires || expires - now <= threshold;
}
