using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace BuildingBlocks.Testing;

public static class TestTokenValidation
{
    public static IServiceCollection AddTestTokenValidation(this IServiceCollection services, string? audience = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            var configuration = new OpenIdConnectConfiguration { Issuer = TestTokens.Issuer };
            configuration.SigningKeys.Add(TestTokens.Key);
            options.Configuration = configuration;
            options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
            options.TokenValidationParameters.ValidIssuer = TestTokens.Issuer;
            options.TokenValidationParameters.ValidAudience = audience ?? TestTokens.DefaultAudience;
        });

        return services;
    }
}
