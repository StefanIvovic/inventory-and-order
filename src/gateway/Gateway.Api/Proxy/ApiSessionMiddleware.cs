using Gateway.Api.Sessions;
using Microsoft.AspNetCore.Authentication;
using Yarp.ReverseProxy.Model;

namespace Gateway.Api.Proxy;

public sealed class ApiSessionMiddleware(RequestDelegate next, TokenRefresher refresher, SessionRevocations revocations, TimeProvider time)
{
    public const string AppHeader = "X-Bff-App";
    public const string SessionMetadata = "Session";
    public const string SessionRequired = "required";
    private const string AccessTokenItem = "gateway.access-token";

    public static bool RequiresSession(IReadOnlyDictionary<string, string>? metadata) =>
        metadata is not null && metadata.TryGetValue(SessionMetadata, out var value) && value == SessionRequired;

    public static string? AccessTokenOf(HttpContext context) =>
        context?.Items.TryGetValue(AccessTokenItem, out var token) == true ? token as string : null;

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var route = context.Features.Get<IReverseProxyFeature>()?.Route.Config;
        if (!RequiresSession(route?.Metadata))
        {
            await next(context);
            return;
        }

        var fetchSite = context.Request.Headers["Sec-Fetch-Site"].ToString();
        if (fetchSite.Length > 0 && fetchSite != "same-origin")
        {
            await Problems.WriteAsync(context, StatusCodes.Status403Forbidden, Problems.CrossSiteRequest);
            return;
        }

        var app = BffApp.FromHeader(context.Request.Headers[AppHeader].ToString());
        if (app is null)
        {
            await Problems.WriteAsync(context, StatusCodes.Status400BadRequest, Problems.AppHeaderRequired);
            return;
        }

        var session = await context.AuthenticateAsync(app.CookieScheme);
        var tokens = session.Properties is null ? null : SessionTokens.From(session.Properties);
        if (!session.Succeeded || session.Principal is null || tokens is null
            || !session.Properties!.Items.TryGetValue(SessionSetup.SessionIdKey, out var sessionId) || string.IsNullOrEmpty(sessionId))
        {
            await Problems.WriteAsync(context, StatusCodes.Status401Unauthorized, Problems.SessionRequired);
            return;
        }

        var result = await refresher.GetCurrentAsync(app, sessionId, tokens, context.RequestAborted);
        switch (result.Outcome)
        {
            case RefreshOutcome.Refused:
                revocations.Revoke(sessionId, session.Properties.ExpiresUtc);
                await context.SignOutAsync(app.CookieScheme);
                await Problems.WriteAsync(context, StatusCodes.Status401Unauthorized, Problems.SessionEnded);
                return;
            case RefreshOutcome.Unavailable:
                await Problems.WriteAsync(context, StatusCodes.Status503ServiceUnavailable, Problems.IdentityUnavailable);
                return;
            case RefreshOutcome.Refreshed:
                var properties = session.Properties;
                result.Tokens!.WriteTo(properties);
                var now = time.GetUtcNow();
                properties.IssuedUtc = now;
                properties.ExpiresUtc = now + SessionSetup.SessionLifetime;
                await context.SignInAsync(app.CookieScheme, session.Principal, properties);
                break;
        }

        context.Items[AccessTokenItem] = result.Tokens!.AccessToken;
        await next(context);
    }
}
