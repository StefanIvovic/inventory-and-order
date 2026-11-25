using System.Security.Cryptography;
using Gateway.Api.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Gateway.Api.Sessions;

public static class SessionSetup
{
    public const string SessionIdKey = "bff_sid";
    public const string SlugKey = "slug";
    public const string TenantParameter = "tenant";
    public static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(12);
    public static readonly IReadOnlyList<string> Scopes = ["openid", "offline_access", "email", "identity", "product", "inventory", "order", "payment"];

    public static IServiceCollection AddBffSessions(this IServiceCollection services, GatewaySettings settings)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(settings);

        services.AddSingleton<SessionRevocations>();
        var authentication = services.AddAuthentication();
        foreach (var app in BffApp.All)
        {
            authentication
                .AddCookie(app.CookieScheme, options => ConfigureCookie(options, app, settings))
                .AddOpenIdConnect(app.OidcScheme, options => ConfigureOidc(options, app, settings));
            services.AddOptions<OpenIdConnectOptions>(app.OidcScheme)
                .Configure<IHttpClientFactory>((options, clients) => options.Backchannel = clients.CreateClient(IdentityBackchannelHandler.ClientName));
        }

        return services;
    }

    public static string NewSessionId() => Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    private static string Base64UrlEncode(byte[] bytes) => System.Buffers.Text.Base64Url.EncodeToString(bytes);

    private static void ConfigureCookie(CookieAuthenticationOptions options, BffApp app, GatewaySettings settings)
    {
        options.Cookie.Name = app.CookieName(settings.IsHttps);
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.Path = "/";
        options.ExpireTimeSpan = SessionLifetime;
        options.SlidingExpiration = false;
        options.Events.OnRedirectToLogin = context => Problems.WriteAsync(context.HttpContext, StatusCodes.Status401Unauthorized, Problems.SessionRequired);
        options.Events.OnRedirectToAccessDenied = context => Problems.WriteAsync(context.HttpContext, StatusCodes.Status401Unauthorized, Problems.SessionRequired);
        options.Events.OnValidatePrincipal = context =>
        {
            var revocations = context.HttpContext.RequestServices.GetRequiredService<SessionRevocations>();
            if (!context.Properties.Items.TryGetValue(SessionIdKey, out var sessionId) || revocations.IsRevoked(sessionId))
            {
                context.RejectPrincipal();
            }

            return Task.CompletedTask;
        };
    }

    private static void ConfigureOidc(OpenIdConnectOptions options, BffApp app, GatewaySettings settings)
    {
        options.Authority = settings.Origin.ToString();
        options.RequireHttpsMetadata = settings.IsHttps;
        options.ClientId = app.ClientId;
        options.ClientSecret = settings.SecretOf(app);
        options.ResponseType = OpenIdConnectResponseType.Code;
        options.ResponseMode = OpenIdConnectResponseMode.FormPost;
        options.UsePkce = true;
        options.SaveTokens = true;
        options.GetClaimsFromUserInfoEndpoint = false;
        options.MapInboundClaims = false;
        options.DisableTelemetry = true;
        options.Scope.Clear();
        foreach (var scope in Scopes)
        {
            options.Scope.Add(scope);
        }

        options.CallbackPath = app.CallbackPath;
        options.SignedOutCallbackPath = app.SignedOutCallbackPath;
        options.SignInScheme = app.CookieScheme;
        options.SignOutScheme = app.CookieScheme;
        options.CorrelationCookie.SameSite = SameSiteMode.Lax;
        options.NonceCookie.SameSite = SameSiteMode.Lax;
        options.TokenValidationParameters.NameClaimType = "sub";

        options.Events.OnRedirectToIdentityProvider = context =>
        {
            if (app.IsStorefront && context.Properties.Items.TryGetValue(SlugKey, out var slug) && slug is not null)
            {
                context.ProtocolMessage.SetParameter(TenantParameter, slug);
            }

            return Task.CompletedTask;
        };
        options.Events.OnTicketReceived = context =>
        {
            if (context.Properties is not null)
            {
                context.Properties.Items[SessionIdKey] = NewSessionId();
                context.Properties.IsPersistent = false;
                context.Properties.AllowRefresh = false;
            }

            return Task.CompletedTask;
        };
        options.Events.OnAccessDenied = context =>
        {
            context.Response.Redirect(FailurePage(app, context.Properties, "access_denied"));
            context.HandleResponse();
            return Task.CompletedTask;
        };
        options.Events.OnRemoteFailure = context =>
        {
            context.Response.Redirect(FailurePage(app, context.Properties, ErrorCode(context.Failure)));
            context.HandleResponse();
            return Task.CompletedTask;
        };
    }

    private static string FailurePage(BffApp app, AuthenticationProperties? properties, string code)
    {
        string? slug = null;
        if (app.IsStorefront && properties?.Items.TryGetValue(SlugKey, out var value) == true && ReturnPaths.IsValidSlug(value))
        {
            slug = value;
        }

        return $"{app.StartPage(slug)}?signin-error={code}";
    }

    private static string ErrorCode(Exception? failure) =>
        (failure?.Data["error"] as string) switch
        {
            "access_denied" => "access_denied",
            "invalid_request" => "invalid_request",
            _ => "server_error",
        };
}
