using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BuildingBlocks.AspNetCore.Tests;

public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string ClaimsHeader = "X-Test-Claims";

    public static string Claims(string? subject, string? tenant, string? userType)
    {
        var parts = new List<string>();
        if (subject is not null)
        {
            parts.Add($"sub={subject}");
        }

        if (tenant is not null)
        {
            parts.Add($"tenant_id={tenant}");
        }

        if (userType is not null)
        {
            parts.Add($"user_type={userType}");
        }

        return string.Join(';', parts);
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ClaimsHeader, out var header))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = header.ToString()
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .Select(pair => new Claim(pair[0], pair.Length > 1 ? pair[1] : string.Empty));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
    }
}
