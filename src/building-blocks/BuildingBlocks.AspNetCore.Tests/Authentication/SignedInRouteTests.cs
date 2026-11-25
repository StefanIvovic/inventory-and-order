using System.Net;
using BuildingBlocks.AspNetCore.Authentication;
using BuildingBlocks.Core.Identity;
using BuildingBlocks.Testing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.IdentityModel.JsonWebTokens;

namespace BuildingBlocks.AspNetCore.Tests.Authentication;

public sealed class SignedInRouteTests
{
    private static readonly Guid UserId = Guid.Parse("4c1d2e3f-5a6b-4c7d-8e9f-0a1b2c3d4e5f");

    private static void Routes(WebApplication app)
    {
        app.MapGet("/signed-in-route", () => "reference").RequireSignedIn();
        app.MapGet("/signed-in-attribute", [SignedIn] () => "reference");
    }

    [Theory]
    [InlineData("/signed-in-route", "staff")]
    [InlineData("/signed-in-route", "customer")]
    [InlineData("/signed-in-route", "platform")]
    [InlineData("/signed-in-attribute", "staff")]
    [InlineData("/signed-in-attribute", "platform")]
    public async Task AnySignedInUserIsAllowed(string path, string caller)
    {
        await using var app = await AuthApp.StartAsync(routes: Routes);
        var token = caller switch
        {
            "staff" => TestTokens.For(UserId, "acme", UserType.Staff, AuthApp.Audience),
            "customer" => TestTokens.For(UserId, "acme", UserType.Customer, AuthApp.Audience),
            _ => TestTokens.Platform(UserId, AuthApp.Audience),
        };

        Assert.Equal(HttpStatusCode.OK, (await app.GetAsync(path, token)).StatusCode);
    }

    [Theory]
    [InlineData("/signed-in-route")]
    [InlineData("/signed-in-attribute")]
    public async Task AnonymousCallerIs401(string path)
    {
        await using var app = await AuthApp.StartAsync(routes: Routes);

        Assert.Equal(HttpStatusCode.Unauthorized, (await app.GetAsync(path)).StatusCode);
    }

    [Theory]
    [InlineData("platform", "acme")]
    [InlineData("staff", null)]
    public async Task MalformedIdentityIs401(string userType, string? tenant)
    {
        await using var app = await AuthApp.StartAsync(routes: Routes);
        var claims = new Dictionary<string, object>
        {
            [AppClaimTypes.Subject] = UserId.ToString(),
            [AppClaimTypes.UserType] = userType,
            [JwtRegisteredClaimNames.Aud] = AuthApp.Audience,
        };
        if (tenant is not null)
        {
            claims[AppClaimTypes.TenantId] = tenant;
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await app.GetAsync("/signed-in-route", TestTokens.Raw(claims))).StatusCode);
    }

    [Fact]
    public void SignedInRoutesAreCategorized()
    {
        var endpoint = new RouteEndpointBuilder(_ => Task.CompletedTask, RoutePatternFactory.Parse("/x"), 0);
        endpoint.Metadata.Add(new SignedInAttribute());

        Assert.Equal(RouteCategory.SignedIn, RouteCategories.Of(endpoint.Build()));
    }

    [Fact]
    public async Task SignedInWithAllowListFailsStartup()
    {
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            AuthApp.StartAsync(routes: a => a.MapGet("/confused-open", () => "x").AllowAnonymous().RequireSignedIn()));

        Assert.Contains("/confused-open", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SignedInWithPlatformFailsStartup()
    {
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            AuthApp.StartAsync(routes: a => a.MapGet("/confused-platform", () => "x").RequirePlatform().RequireSignedIn()));

        Assert.Contains("/confused-platform", error.Message, StringComparison.Ordinal);
    }
}
