using System.Net.Http.Headers;
using Gateway.Api.Configuration;
using Gateway.Api.Sessions;
using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;

namespace Gateway.Api.Proxy;

public static class ProxyTransforms
{
    private static readonly string[] OidcCookiePrefixes = [".AspNetCore.Correlation.", ".AspNetCore.OpenIdConnect.Nonce."];

    public static void Apply(TransformBuilderContext builder, GatewaySettings settings)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(settings);

        if (ApiSessionMiddleware.RequiresSession(builder.Route.Metadata))
        {
            builder.AddRequestTransform(context =>
            {
                var headers = context.ProxyRequest.Headers;
                headers.Remove("Cookie");
                headers.Remove("Authorization");
                headers.Remove(ApiSessionMiddleware.AppHeader);
                var token = ApiSessionMiddleware.AccessTokenOf(context.HttpContext)
                    ?? throw new InvalidOperationException("An API route was forwarded without a session token.");
                headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                return ValueTask.CompletedTask;
            });
            return;
        }

        var sessionCookies = BffApp.All.Select(app => app.CookieName(settings.IsHttps)).ToArray();
        builder.AddRequestTransform(context =>
        {
            var headers = context.ProxyRequest.Headers;
            if (!headers.TryGetValues("Cookie", out var values))
            {
                return ValueTask.CompletedTask;
            }

            var kept = values
                .SelectMany(value => value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Where(pair => !IsGatewayCookie(pair, sessionCookies))
                .ToList();
            headers.Remove("Cookie");
            if (kept.Count > 0)
            {
                headers.TryAddWithoutValidation("Cookie", string.Join("; ", kept));
            }

            return ValueTask.CompletedTask;
        });
    }

    private static bool IsGatewayCookie(string pair, string[] sessionCookies)
    {
        var name = pair.Split('=', 2)[0];
        return sessionCookies.Any(cookie => name.StartsWith(cookie, StringComparison.Ordinal))
            || OidcCookiePrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal));
    }
}
