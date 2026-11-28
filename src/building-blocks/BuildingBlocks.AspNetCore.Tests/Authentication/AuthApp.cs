using BuildingBlocks.AspNetCore.Authentication;
using BuildingBlocks.Testing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.AspNetCore.Tests.Authentication;

public sealed class AuthApp : IAsyncDisposable
{
    public const string Audience = "svc";

    private readonly WebApplication app;

    private AuthApp(WebApplication app)
    {
        this.app = app;
        Client = app.GetTestClient();
    }

    public HttpClient Client { get; }

    public static async Task<AuthApp> StartAsync(
        string authority = TestTokens.Issuer,
        bool trustTestKey = true,
        Action<IServiceCollection>? services = null,
        Action<WebApplication>? routes = null,
        string? internalAddress = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Configuration["Authentication:Authority"] = authority;
        builder.Configuration["Authentication:Audience"] = Audience;
        builder.Configuration["Authentication:RequireHttpsMetadata"] = "false";
        if (internalAddress is not null)
        {
            builder.Configuration["Authentication:InternalAddress"] = internalAddress;
        }

        builder.Services.AddBuildingBlocks();
        builder.Services.AddBuildingBlocksAuthentication(builder.Configuration);
        if (trustTestKey)
        {
            builder.Services.AddTestTokenValidation(Audience);
        }

        services?.Invoke(builder.Services);

        var app = builder.Build();
        app.UseBuildingBlocksAuthentication();
        app.MapGet("/tenant-route", () => "tenant");
        app.MapGet("/platform-route", () => "platform").RequirePlatform();
        app.MapGet("/platform-attribute", [PlatformOnly] () => "platform");
        app.MapGet("/open", () => "open").AllowAnonymous();
        routes?.Invoke(app);

        await app.StartAsync(TestContext.Current.CancellationToken);
        return new AuthApp(app);
    }

    public async Task<HttpResponseMessage> GetAsync(string path, string? token = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (token is not null)
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }

        return await Client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await app.DisposeAsync();
    }
}
