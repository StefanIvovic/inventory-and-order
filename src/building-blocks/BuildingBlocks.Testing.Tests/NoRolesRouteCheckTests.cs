using BuildingBlocks.AspNetCore;
using BuildingBlocks.AspNetCore.Authentication;
using BuildingBlocks.Core.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Testing.Tests;

public sealed class NoRolesRouteCheckTests
{
    private const string Audience = "svc";

    [Fact]
    public async Task GuardedRoutesPassAndAllowListedRoutesAreSkipped()
    {
        await using var app = await StartAsync(a =>
        {
            a.MapGet("/guarded", Guard);
            a.MapGet("/items/{id:guid}", (Guid id, ICurrentUser user) => id == Guid.Empty ? Results.BadRequest() : Guard(user));
            a.MapGet("/pages/{page:int}", (int page, ICurrentUser user) => Guard(user));
            a.MapPost("/items", (ItemBody body, ICurrentUser user) => Guard(user));
            a.MapGet("/platform", (ICurrentUser user) => user.UserType == UserType.Platform ? Results.StatusCode(403) : Results.StatusCode(500)).RequirePlatform();
            a.MapGet("/health", () => "ok").AllowAnonymous();
        });

        var report = await NoRolesRouteCheck.RunAsync(app.Services, app.GetTestClient(), Audience, TestContext.Current.CancellationToken);

        Assert.Empty(report.Failures);
        Assert.Contains("GET /guarded (Tenant)", report.Checked);
        Assert.Contains("GET /items/{id:guid} (Tenant)", report.Checked);
        Assert.Contains("GET /pages/{page:int} (Tenant)", report.Checked);
        Assert.Contains("POST /items (Tenant)", report.Checked);
        Assert.Contains("GET /platform (Platform)", report.Checked);
        Assert.Equal(["GET /health"], report.Skipped);
    }

    [Fact]
    public async Task RouteWithoutPermissionCheckIsReported()
    {
        await using var app = await StartAsync(a =>
        {
            a.MapGet("/guarded", Guard);
            a.MapGet("/leaky", () => "secret");
        });

        var report = await NoRolesRouteCheck.RunAsync(app.Services, app.GetTestClient(), Audience, TestContext.Current.CancellationToken);

        var failure = Assert.Single(report.Failures);
        Assert.Equal("GET /leaky (Tenant) returned 200 instead of 403", failure);
        Assert.Contains("Failures (1):", report.ToString(), StringComparison.Ordinal);
    }

    private static IResult Guard(ICurrentUser user) =>
        user.TenantId is not null && user.UserType == UserType.Staff ? Results.StatusCode(403) : Results.StatusCode(500);

    private static async Task<WebApplication> StartAsync(Action<WebApplication> routes)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Configuration["Authentication:Authority"] = TestTokens.Issuer;
        builder.Configuration["Authentication:Audience"] = Audience;
        builder.Services.AddBuildingBlocks();
        builder.Services.AddBuildingBlocksAuthentication(builder.Configuration);
        builder.Services.AddTestTokenValidation(Audience);

        var app = builder.Build();
        app.UseBuildingBlocksAuthentication();
        routes(app);
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    public sealed record ItemBody(string? Name);
}
