using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Gateway.Tests.Fakes;

public enum RefreshMode
{
    Normal,
    Refuse,
    Fail,
}

public sealed record FakeUser(string Subject, string UserType, string? TenantId, string? Email);

public sealed class FakeIdentity(Uri issuer, IReadOnlyDictionary<string, (string Secret, string RedirectUri, string PostLogoutUri)> clients) : FakeServer
{
    private readonly RsaSecurityKey key = new(RSA.Create(2048)) { KeyId = "fake-key" };
    private readonly ConcurrentDictionary<string, IssuedCode> codes = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, (string ClientId, string AuthorizationId)> refreshTokens = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> idTokens = new(StringComparer.Ordinal);
    private int refreshCount;

    public FakeUser User { get; set; } = new("user-1", "staff", "tenant-1", "staff@acme.test");

    public bool DenySignIn { get; set; }

    public RefreshMode Refresh { get; set; } = RefreshMode.Normal;

    public TimeSpan RefreshDelay { get; set; } = TimeSpan.Zero;

    public int AccessTokenSeconds { get; set; } = 900;

    public int RefreshCount => Volatile.Read(ref refreshCount);

    public ConcurrentQueue<string> IssuedAccessTokens { get; } = new();

    public string Authority => issuer.ToString();

    public int ActiveRefreshTokens(string clientId) => refreshTokens.Values.Count(owner => owner.ClientId == clientId);

    protected override void Map(WebApplication app)
    {
        app.MapGet("/.well-known/openid-configuration", () => Results.Json(new Dictionary<string, object>
        {
            ["issuer"] = Authority,
            ["authorization_endpoint"] = Authority + "connect/authorize",
            ["token_endpoint"] = Authority + "connect/token",
            ["end_session_endpoint"] = Authority + "connect/endsession",
            ["jwks_uri"] = Authority + ".well-known/jwks",
            ["response_types_supported"] = new List<string> { "code" },
            ["response_modes_supported"] = new List<string> { "form_post", "query" },
            ["subject_types_supported"] = new List<string> { "public" },
            ["id_token_signing_alg_values_supported"] = new List<string> { "RS256" },
            ["code_challenge_methods_supported"] = new List<string> { "S256" },
        }));
        app.MapGet("/.well-known/jwks", () =>
        {
            var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(key);
            return Results.Json(new { keys = new[] { new { kty = jwk.Kty, kid = jwk.Kid, use = "sig", alg = "RS256", n = jwk.N, e = jwk.E } } });
        });
        app.MapGet("/connect/authorize", Authorize);
        app.MapPost("/connect/token", (Func<HttpContext, Task<IResult>>)TokenAsync);
        app.MapGet("/connect/endsession", EndSession);
        app.MapGet("/status/{code:int}", (int code) => Results.Problem(statusCode: code, type: "upstream-" + code, title: "status " + code));
        app.MapFallback("{**path}", Echo);
    }

    private IResult Authorize(HttpContext context)
    {
        var query = context.Request.Query;
        var clientId = query["client_id"].ToString();
        var redirect = query["redirect_uri"].ToString();
        if (!clients.TryGetValue(clientId, out var client) || client.RedirectUri != redirect
            || query["response_type"] != "code" || query["code_challenge_method"] != "S256")
        {
            return Results.BadRequest("invalid authorization request");
        }

        var fields = new Dictionary<string, string> { ["state"] = query["state"].ToString() };
        if (DenySignIn)
        {
            fields["error"] = "access_denied";
        }
        else
        {
            var code = Guid.NewGuid().ToString("N");
            codes[code] = new IssuedCode(clientId, redirect, query["code_challenge"].ToString(), query["nonce"].ToString(), User, Guid.NewGuid().ToString("N"));
            fields["code"] = code;
        }

        var inputs = string.Concat(fields.Select(field => $"<input type=\"hidden\" name=\"{field.Key}\" value=\"{WebUtility.HtmlEncode(field.Value)}\" />"));
        return Results.Content($"<html><body><form method=\"post\" action=\"{WebUtility.HtmlEncode(redirect)}\">{inputs}</form></body></html>", "text/html");
    }

    private async Task<IResult> TokenAsync(HttpContext context)
    {
        var form = await context.Request.ReadFormAsync();
        var clientId = form["client_id"].ToString();
        if (!clients.TryGetValue(clientId, out var client) || client.Secret != form["client_secret"])
        {
            return Results.Json(new { error = "invalid_client" }, statusCode: 401);
        }

        switch (form["grant_type"].ToString())
        {
            case "authorization_code":
                if (!codes.TryRemove(form["code"].ToString(), out var issued) || issued.ClientId != clientId || issued.RedirectUri != form["redirect_uri"]
                    || issued.Challenge != Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(form["code_verifier"].ToString()))))
                {
                    return Results.Json(new { error = "invalid_grant" }, statusCode: 400);
                }

                return Results.Json(Issue(clientId, issued.User, issued.Nonce, issued.AuthorizationId));
            case "refresh_token":
                Interlocked.Increment(ref refreshCount);
                if (RefreshDelay > TimeSpan.Zero)
                {
                    await Task.Delay(RefreshDelay);
                }

                if (Refresh == RefreshMode.Fail)
                {
                    return Results.StatusCode(500);
                }

                if (Refresh == RefreshMode.Refuse || !refreshTokens.TryRemove(form["refresh_token"].ToString(), out var owner) || owner.ClientId != clientId)
                {
                    return Results.Json(new { error = "invalid_grant" }, statusCode: 400);
                }

                return Results.Json(Issue(clientId, User, null, owner.AuthorizationId));
            default:
                return Results.Json(new { error = "unsupported_grant_type" }, statusCode: 400);
        }
    }

    private IResult EndSession(HttpContext context)
    {
        var redirect = context.Request.Query["post_logout_redirect_uri"].ToString();
        if (!clients.Values.Any(client => client.PostLogoutUri == redirect))
        {
            return Results.BadRequest("unregistered post_logout_redirect_uri");
        }

        if (idTokens.TryGetValue(context.Request.Query["id_token_hint"].ToString(), out var authorization))
        {
            foreach (var (token, owner) in refreshTokens)
            {
                if (owner.AuthorizationId == authorization)
                {
                    refreshTokens.TryRemove(token, out _);
                }
            }
        }

        return Results.Redirect(QueryHelpers.AddQueryString(redirect, "state", context.Request.Query["state"].ToString()));
    }

    private Dictionary<string, object> Issue(string clientId, FakeUser user, string? nonce, string authorizationId)
    {
        var accessToken = "at-" + Guid.NewGuid().ToString("N");
        var refreshToken = "rt-" + Guid.NewGuid().ToString("N");
        refreshTokens[refreshToken] = (clientId, authorizationId);
        IssuedAccessTokens.Enqueue(accessToken);

        var claims = new Dictionary<string, object>
        {
            ["sub"] = user.Subject,
            ["user_type"] = user.UserType,
            ["at_hash"] = Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(accessToken))[..16]),
        };
        if (user.TenantId is not null)
        {
            claims["tenant_id"] = user.TenantId;
        }

        if (user.Email is not null)
        {
            claims["email"] = user.Email;
        }

        if (nonce is not null)
        {
            claims["nonce"] = nonce;
        }

        var now = DateTime.UtcNow;
        var idToken = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Authority,
            Audience = clientId,
            IssuedAt = now,
            NotBefore = now,
            Expires = now.AddHours(1),
            Claims = claims,
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256),
        });

        idTokens[idToken] = authorizationId;
        return new Dictionary<string, object>
        {
            ["access_token"] = accessToken,
            ["token_type"] = "Bearer",
            ["expires_in"] = AccessTokenSeconds,
            ["refresh_token"] = refreshToken,
            ["id_token"] = idToken,
        };
    }

    private sealed record IssuedCode(string ClientId, string RedirectUri, string Challenge, string Nonce, FakeUser User, string AuthorizationId);
}
