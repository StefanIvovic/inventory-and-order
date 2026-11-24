using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using BuildingBlocks.AspNetCore.Authentication;
using BuildingBlocks.Core.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BuildingBlocks.AspNetCore.Tests.Authentication;

public sealed class CustomSchemeTests
{
    private const string SchemeName = "custom";
    private const string Header = "X-Caller";

    [Theory]
    [InlineData("/tenant-route", null, HttpStatusCode.Unauthorized)]
    [InlineData("/tenant-route", "staff", HttpStatusCode.OK)]
    [InlineData("/tenant-route", "platform", HttpStatusCode.Forbidden)]
    [InlineData("/platform-route", "platform", HttpStatusCode.OK)]
    [InlineData("/platform-route", "staff", HttpStatusCode.Forbidden)]
    [InlineData("/open", null, HttpStatusCode.OK)]
    public async Task RouteCategoriesWorkWithAnyScheme(string path, string? caller, HttpStatusCode expected)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddBuildingBlocks();
        builder.Services.AddAuthentication(SchemeName).AddScheme<AuthenticationSchemeOptions, HeaderHandler>(SchemeName, null);
        builder.Services.AddBuildingBlocksRouteAuthorization(SchemeName);
        await using var app = builder.Build();
        app.UseBuildingBlocksAuthentication();
        app.MapGet("/tenant-route", () => "tenant");
        app.MapGet("/platform-route", () => "platform").RequirePlatform();
        app.MapGet("/open", () => "open").AllowAnonymous();
        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = app.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (caller is not null)
        {
            request.Headers.Add(Header, caller);
        }

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(expected, response.StatusCode);
    }

    private sealed class HeaderHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(Header, out var caller))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var claims = new List<Claim>
            {
                new(AppClaimTypes.Subject, Guid.NewGuid().ToString()),
                new(AppClaimTypes.UserType, caller.ToString()),
            };
            if (caller != "platform")
            {
                claims.Add(new Claim(AppClaimTypes.TenantId, "acme"));
            }

            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
        }
    }
}
