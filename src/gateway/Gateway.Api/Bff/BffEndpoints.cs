using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Gateway.Api.Sessions;
using Microsoft.AspNetCore.Authentication;

namespace Gateway.Api.Bff;

public static class BffEndpoints
{
    public static IEndpointRouteBuilder MapBffEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var backoffice = endpoints.MapGroup("/bff/backoffice");
        backoffice.MapGet("/login", (string? returnUrl) => Login(BffApp.Backoffice, null, returnUrl));
        backoffice.MapGet("/user", (Func<HttpContext, Task<IResult>>)(context => UserAsync(context, BffApp.Backoffice, null)));
        backoffice.MapGet("/logout", (HttpContext context, string? sid) => LogoutAsync(context, BffApp.Backoffice, null, sid));

        var shop = endpoints.MapGroup($"/bff/shop/{{slug:{SlugConstraint.Name}}}");
        shop.MapGet("/login", (string slug, string? returnUrl) => Login(BffApp.Storefront, slug, returnUrl));
        shop.MapGet("/user", (HttpContext context, string slug) => UserAsync(context, BffApp.Storefront, slug));
        shop.MapGet("/logout", (HttpContext context, string slug, string? sid) => LogoutAsync(context, BffApp.Storefront, slug, sid));

        return endpoints;
    }

    private static IResult Login(BffApp app, string? slug, string? returnUrl)
    {
        var properties = new AuthenticationProperties { RedirectUri = ReturnPaths.Resolve(returnUrl, app.StartPage(slug)) };
        if (slug is not null)
        {
            properties.Items[SessionSetup.SlugKey] = slug;
        }

        return Results.Challenge(properties, [app.OidcScheme]);
    }

    private static async Task<IResult> UserAsync(HttpContext context, BffApp app, string? slug)
    {
        var session = await AuthenticateAsync(context, app, slug);
        if (session is null)
        {
            return Problems.Result(StatusCodes.Status401Unauthorized, Problems.SessionRequired);
        }

        var (principal, properties) = session.Value;
        var sid = properties.Items[SessionSetup.SessionIdKey]!;
        return Results.Ok(new SessionUser(
            principal.FindFirst("sub")?.Value ?? string.Empty,
            principal.FindFirst("user_type")?.Value ?? string.Empty,
            principal.FindFirst("tenant_id")?.Value,
            principal.FindFirst("email")?.Value,
            $"{app.BffPrefix(slug)}/logout?sid={Uri.EscapeDataString(sid)}",
            slug));
    }

    private static async Task<IResult> LogoutAsync(HttpContext context, BffApp app, string? slug, string? sid)
    {
        var start = app.StartPage(slug);
        var session = await AuthenticateAsync(context, app, slug);
        if (session is null)
        {
            return Results.Redirect(start);
        }

        var expected = session.Value.Properties.Items[SessionSetup.SessionIdKey]!;
        if (sid is null || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(sid), Encoding.UTF8.GetBytes(expected)))
        {
            return Problems.Result(StatusCodes.Status400BadRequest, Problems.LogoutProofRequired);
        }

        context.RequestServices.GetRequiredService<SessionRevocations>().Revoke(expected, session.Value.Properties.ExpiresUtc);
        await context.SignOutAsync(app.OidcScheme, new AuthenticationProperties { RedirectUri = start });
        await context.SignOutAsync(app.CookieScheme);
        return Results.Empty;
    }

    private static async Task<(System.Security.Claims.ClaimsPrincipal Principal, AuthenticationProperties Properties)?> AuthenticateAsync(HttpContext context, BffApp app, string? slug)
    {
        var result = await context.AuthenticateAsync(app.CookieScheme);
        if (!result.Succeeded || result.Principal is null || result.Properties is null
            || !result.Properties.Items.TryGetValue(SessionSetup.SessionIdKey, out var sid) || string.IsNullOrEmpty(sid))
        {
            return null;
        }

        if (app.IsStorefront && (!result.Properties.Items.TryGetValue(SessionSetup.SlugKey, out var sessionSlug) || sessionSlug != slug))
        {
            return null;
        }

        return (result.Principal, result.Properties);
    }

    private sealed record SessionUser(
        string UserId,
        string UserType,
        string? TenantId,
        string? Email,
        string LogoutUrl,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Slug);
}
