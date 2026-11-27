using BuildingBlocks.Core.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BuildingBlocks.AspNetCore.Authentication;

public static class AuthenticationExtensions
{
    public static IServiceCollection AddBuildingBlocksAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<AuthenticationSettings>()
            .Bind(configuration.GetSection(AuthenticationSettings.SectionName))
            .Validate(settings => !string.IsNullOrWhiteSpace(settings.Authority), "Authentication:Authority is required.")
            .Validate(settings => !string.IsNullOrWhiteSpace(settings.Audience), "Authentication:Audience is required.")
            .Validate(
                settings => settings.InternalAddress is null || AuthenticationSettings.IsOrigin(settings.InternalAddress),
                "Authentication:InternalAddress must be an absolute http or https address with no path.")
            .ValidateOnStart();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<AuthenticationSettings>>((options, settings) =>
            {
                options.Authority = settings.Value.Authority;
                options.Audience = settings.Value.Audience;
                options.RequireHttpsMetadata = settings.Value.RequireHttpsMetadata;
                options.MapInboundClaims = false;
                options.RefreshOnIssuerKeyNotFound = true;
                options.TokenValidationParameters.ValidateIssuer = true;
                options.TokenValidationParameters.ValidateAudience = true;
                options.TokenValidationParameters.ValidateLifetime = true;
                options.TokenValidationParameters.ValidateIssuerSigningKey = true;
                options.TokenValidationParameters.NameClaimType = AppClaimTypes.Subject;
                if (settings.Value.InternalAddress is { } internalAddress && AuthenticationSettings.IsOrigin(internalAddress))
                {
                    options.BackchannelHttpHandler = new IssuerBackchannelHandler(new Uri(settings.Value.Authority), new Uri(internalAddress))
                    {
                        InnerHandler = new SocketsHttpHandler(),
                    };
                }
            });

        return services.AddBuildingBlocksRouteAuthorization(JwtBearerDefaults.AuthenticationScheme);
    }

    public static IServiceCollection AddBuildingBlocksRouteAuthorization(this IServiceCollection services, string authenticationScheme)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(authenticationScheme);

        var tenant = Policy(authenticationScheme, RouteCategory.Tenant);
        services.AddAuthorizationBuilder()
            .AddPolicy(AuthorizationPolicies.Tenant, tenant)
            .AddPolicy(AuthorizationPolicies.Platform, Policy(authenticationScheme, RouteCategory.Platform))
            .AddPolicy(AuthorizationPolicies.SignedIn, Policy(authenticationScheme, RouteCategory.SignedIn))
            .SetDefaultPolicy(tenant)
            .SetFallbackPolicy(tenant);

        services.AddHostedService<RouteCategoryValidator>();
        return services;
    }

    public static IApplicationBuilder UseBuildingBlocksAuthentication(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseAuthentication();
        app.UseBuildingBlocks();
        app.UseAuthorization();
        return app;
    }

    public static TBuilder RequirePlatform<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithMetadata(new PlatformOnlyAttribute());
    }

    public static TBuilder RequireSignedIn<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithMetadata(new SignedInAttribute());
    }

    private static AuthorizationPolicy Policy(string authenticationScheme, RouteCategory category) =>
        new AuthorizationPolicyBuilder(authenticationScheme)
            .RequireAuthenticatedUser()
            .AddRequirements(new RouteCategoryRequirement(category))
            .Build();
}
