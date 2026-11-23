using System.Security.Claims;
using System.Text.Encodings.Web;
using BuildingBlocks.Core.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BuildingBlocks.Messaging.Tests;

public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string UserHeader = "X-Test-User";
    public const string TenantHeader = "X-Test-Tenant";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(UserHeader, out var user))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new List<Claim> { new(AppClaimTypes.Subject, user.ToString()) };
        if (Request.Headers.TryGetValue(TenantHeader, out var tenant))
        {
            claims.Add(new Claim(AppClaimTypes.UserType, "staff"));
            claims.Add(new Claim(AppClaimTypes.TenantId, tenant.ToString()));
        }
        else
        {
            claims.Add(new Claim(AppClaimTypes.UserType, "platform"));
        }
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
    }
}
