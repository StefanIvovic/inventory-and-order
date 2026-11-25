using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Gateway.Api.Configuration;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;

namespace Gateway.Api.Sessions;

public enum RefreshOutcome
{
    Current,
    Refreshed,
    Refused,
    Unavailable,
}

public sealed record RefreshResult(RefreshOutcome Outcome, SessionTokens? Tokens)
{
    public static readonly RefreshResult Refused = new(RefreshOutcome.Refused, null);
    public static readonly RefreshResult Unavailable = new(RefreshOutcome.Unavailable, null);
}

public sealed partial class TokenRefresher(
    IHttpClientFactory clients,
    IOptionsMonitor<OpenIdConnectOptions> oidc,
    GatewaySettings settings,
    TimeProvider time,
    ILogger<TokenRefresher> logger)
{
    public static readonly TimeSpan ResultLifetime = TimeSpan.FromMinutes(5);

    private readonly ConcurrentDictionary<string, Lazy<Task<RefreshResult>>> inFlight = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, (RefreshResult Result, DateTimeOffset Expires)> results = new(StringComparer.Ordinal);

    public async Task<RefreshResult> GetCurrentAsync(BffApp app, string sessionId, SessionTokens tokens, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(tokens);

        var now = time.GetUtcNow();
        if (!tokens.NeedsRefresh(now, settings.Refresh.Threshold))
        {
            return new RefreshResult(RefreshOutcome.Current, tokens);
        }

        Prune(now);
        var key = KeyOf(sessionId, tokens.RefreshToken);
        if (results.TryGetValue(key, out var cached) && cached.Expires > now)
        {
            return cached.Result;
        }

        var lazy = inFlight.GetOrAdd(key, _ => new Lazy<Task<RefreshResult>>(() => RedeemAndRememberAsync(app, key, tokens)));
        try
        {
            return await lazy.Value.WaitAsync(cancellationToken);
        }
        finally
        {
            if (lazy.Value.IsCompleted)
            {
                inFlight.TryRemove(new KeyValuePair<string, Lazy<Task<RefreshResult>>>(key, lazy));
            }
        }
    }

    private async Task<RefreshResult> RedeemAndRememberAsync(BffApp app, string key, SessionTokens tokens)
    {
        var result = await RedeemAsync(app, tokens);
        if (result.Outcome != RefreshOutcome.Unavailable)
        {
            results[key] = (result, time.GetUtcNow() + ResultLifetime);
        }

        return result;
    }

    private async Task<RefreshResult> RedeemAsync(BffApp app, SessionTokens tokens)
    {
        try
        {
            var options = oidc.Get(app.OidcScheme);
            var configuration = await options.ConfigurationManager!.GetConfigurationAsync(CancellationToken.None);
            using var client = clients.CreateClient(IdentityBackchannelHandler.ClientName);
            using var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = tokens.RefreshToken,
                ["client_id"] = app.ClientId,
                ["client_secret"] = settings.SecretOf(app),
            });
            using var response = await client.PostAsync(new Uri(configuration.TokenEndpoint), form, CancellationToken.None);
            if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized)
            {
                LogRefused(logger, app.Name);
                return RefreshResult.Refused;
            }

            if (!response.IsSuccessStatusCode)
            {
                LogUnexpectedStatus(logger, (int)response.StatusCode, app.Name);
                return RefreshResult.Unavailable;
            }

            var body = await response.Content.ReadFromJsonAsync<TokenResponse>(CancellationToken.None);
            if (body?.AccessToken is null)
            {
                return RefreshResult.Unavailable;
            }

            var refreshed = new SessionTokens(
                body.AccessToken,
                body.RefreshToken ?? tokens.RefreshToken,
                body.IdToken ?? tokens.IdToken,
                time.GetUtcNow() + TimeSpan.FromSeconds(body.ExpiresIn ?? 0));
            return new RefreshResult(RefreshOutcome.Refreshed, refreshed);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException or IOException)
        {
            LogFailed(logger, exception, app.Name);
            return RefreshResult.Unavailable;
        }
    }

    private void Prune(DateTimeOffset now)
    {
        foreach (var (key, entry) in results)
        {
            if (entry.Expires <= now)
            {
                results.TryRemove(key, out _);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Identity refused a token refresh for {App}.")]
    private static partial void LogRefused(ILogger logger, string app);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Identity answered {Status} to a token refresh for {App}.")]
    private static partial void LogUnexpectedStatus(ILogger logger, int status, string app);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Token refresh for {App} failed.")]
    private static partial void LogFailed(ILogger logger, Exception exception, string app);

    private static string KeyOf(string sessionId, string refreshToken) =>
        sessionId + ":" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("refresh_token")] string? RefreshToken,
        [property: JsonPropertyName("id_token")] string? IdToken,
        [property: JsonPropertyName("expires_in")] int? ExpiresIn);
}
