using BuildingBlocks.AspNetCore;
using BuildingBlocks.Core.Permissions;
using BuildingBlocks.Core.Tenancy;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.Http;

namespace BuildingBlocks.Messaging.Tests;

public sealed class TestServiceHost : IAsyncDisposable
{
    private TestServiceHost(WebApplication app, string serviceName, string connectionString)
    {
        App = app;
        ServiceName = serviceName;
        ConnectionString = connectionString;
        Recorder = app.Services.GetRequiredService<Recorder>();
        ProviderCalls = app.Services.GetRequiredService<ProviderCalls>();
        Logs = app.Services.GetRequiredService<CapturingLoggerProvider>();
    }

    public WebApplication App { get; }

    public string ServiceName { get; }

    public string ConnectionString { get; }

    public Recorder Recorder { get; }

    public ProviderCalls ProviderCalls { get; }

    public CapturingLoggerProvider Logs { get; }

    public HttpClient CreateClient() => App.GetTestClient();

    public static string UniqueName(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..(prefix.Length + 9)];

    public static async Task<TestServiceHost> StartAsync(
        string serviceName,
        string connectionString,
        Uri rabbitMqUri,
        Action<WolverineOptions> routing,
        ServiceHostOptions? hostOptions = null)
    {
        var settings = hostOptions ?? new ServiceHostOptions();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        var logs = new CapturingLoggerProvider();
        builder.Logging.AddProvider(logs);
        builder.Services.AddSingleton(logs);

        builder.Services.AddBuildingBlocks();
        if (settings.DbPermissions)
        {
            builder.Services.AddBuildingBlocksPermissions<DbPermissionProvider>();
        }
        else
        {
            builder.Services.AddBuildingBlocksPermissions<StaticPermissionProvider>();
        }

        builder.Services.AddSingleton<ProviderCalls>();
        builder.Services.AddSingleton<Recorder>();
        builder.Services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, null);
        builder.Services.AddDbContextWithWolverineIntegration<TestDbContext>(options => options.UseNpgsql(connectionString));
        builder.Services.AddWolverineHttp();

        builder.Host.UseWolverine(options =>
        {
            options.ApplicationAssembly = typeof(TestServiceHost).Assembly;
            options.UseBuildingBlocksMessaging(new MessagingSettings(serviceName, connectionString, rabbitMqUri));
            options.Durability.Mode = settings.Durability;
            if (settings.PermissionInvalidation)
            {
                options.UseBuildingBlocksPermissionInvalidation();
            }

            routing(options);
        });

        var app = builder.Build();
        app.UseAuthentication();
        app.UseBuildingBlocks();
        app.MapPostToWolverine<CreateWidget, WidgetCreatedResponse>("/widgets");
        app.MapGet("/permissions", async (IPermissionManager permissions, CancellationToken cancellationToken) =>
            (await permissions.GetGrantedAsync(cancellationToken)).Order(StringComparer.Ordinal).ToArray());

        await using (var scope = app.Services.CreateAsyncScope())
        {
            var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<TestDbContext>>();
            await using var db = new TestDbContext(options, new TenantContext());
            await db.Database.EnsureCreatedAsync();
        }

        await app.StartAsync();
        return new TestServiceHost(app, serviceName, connectionString);
    }

    public async Task SeedAsync(string tenant, params string[] names)
    {
        await using var scope = App.Services.CreateAsyncScope();
        var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<TestDbContext>>();
        var tenantContext = new TenantContext();
        tenantContext.Set(tenant);
        await using var db = new TestDbContext(options, tenantContext);
        db.Widgets.AddRange(names.Select(n => Widget.Create(n)));
        await db.SaveChangesAsync();
    }

    public async Task<int> CountAllWidgetsAsync()
    {
        await using var scope = App.Services.CreateAsyncScope();
        var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<TestDbContext>>();
        await using var db = new TestDbContext(options, new TenantContext());
        return await db.Widgets.IgnoreQueryFilters().CountAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await App.StopAsync();
        await App.DisposeAsync();
    }
}

public sealed record ServiceHostOptions(DurabilityMode Durability = DurabilityMode.Solo, bool DbPermissions = false, bool PermissionInvalidation = false);
