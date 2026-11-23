using System.Net.Http.Json;
using System.Text.Json;
using BuildingBlocks.Core.Errors;
using BuildingBlocks.Core.Identity;
using BuildingBlocks.Core.Tenancy;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.AspNetCore.Tests;

public sealed class InvocationCounter
{
    private int count;

    public int Count => count;

    public void Increment() => Interlocked.Increment(ref count);
}

public sealed class TestApp : IAsyncDisposable
{
    public const string Secret = "SECRET-123";

    private readonly WebApplication app;

    private TestApp(WebApplication app, CapturingLoggerProvider logs)
    {
        this.app = app;
        Logs = logs;
        Client = app.GetTestClient();
        Invocations = app.Services.GetRequiredService<InvocationCounter>();
    }

    public HttpClient Client { get; }

    public CapturingLoggerProvider Logs { get; }

    public InvocationCounter Invocations { get; }

    public static async Task<TestApp> StartAsync()
    {
        var logs = new CapturingLoggerProvider();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(logs);
        builder.Logging.SetMinimumLevel(LogLevel.Trace);

        builder.Services.AddBuildingBlocks();
        builder.Services.AddSingleton<InvocationCounter>();
        builder.Services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, null);

        var app = builder.Build();

        app.Use(async (context, next) =>
        {
            if (context.Request.Path == "/early")
            {
                throw new InvalidOperationException(Secret);
            }

            await next(context);
        });

        app.UseAuthentication();
        app.UseBuildingBlocks();

        app.MapGet("/me", (ICurrentUser user, InvocationCounter counter) =>
        {
            counter.Increment();
            return user.IsAuthenticated
                ? Results.Ok(new { authenticated = true, userId = user.UserId, tenantId = user.TenantId, userType = user.UserType.ToString() })
                : Results.Ok(new { authenticated = false });
        });

        app.MapGet("/me/id", (ICurrentUser user) =>
        {
            try
            {
                _ = user.UserId;
                return "returned";
            }
            catch (InvalidOperationException)
            {
                return "threw";
            }
        });

        app.MapGet("/tenant", (ITenantContext tenant) => tenant.HasTenant ? tenant.TenantId! : "<none>");
        app.MapGet("/conflict", () => Throw(new DomainException("order.cancel.already-shipped", DomainErrorKind.Conflict, "The order has already shipped.")));
        app.MapGet("/rule", () => Throw(new DomainException("inventory.transfer.insufficient-stock", DomainErrorKind.Rule, "Not enough stock in the source warehouse.")));
        app.MapGet("/missing", () => Throw(new NotFoundException("order")));
        app.MapGet("/boom", () => Throw(new InvalidOperationException(Secret)));

        await app.StartAsync();
        return new TestApp(app, logs);
    }

    public static HttpRequestMessage Get(string path, string? claims = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (claims is not null)
        {
            request.Headers.Add(TestAuthHandler.ClaimsHeader, claims);
        }

        return request;
    }

    public static async Task<JsonElement> JsonAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await app.StopAsync();
        await app.DisposeAsync();
    }

    private static string Throw(Exception exception) => throw exception;
}
