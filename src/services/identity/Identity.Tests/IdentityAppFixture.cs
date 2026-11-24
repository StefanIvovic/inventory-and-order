using BuildingBlocks.Core.Tenancy;
using BuildingBlocks.Testing;
using Identity.Api.Persistence;
using JasperFx.CommandLine;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

[assembly: AssemblyFixture(typeof(Identity.Tests.ContainersFixture))]

namespace Identity.Tests;

public sealed class ContainersFixture : IAsyncLifetime
{
    public PostgresFixture Postgres { get; } = new();

    public RabbitMqFixture RabbitMq { get; } = new();

    public async ValueTask InitializeAsync()
    {
        JasperFxEnvironment.AutoStartHost = true;
        await Postgres.InitializeAsync();
        await RabbitMq.InitializeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await RabbitMq.DisposeAsync();
        await Postgres.DisposeAsync();
    }
}

public sealed class IdentityApp : WebApplicationFactory<Program>
{
    public const string Audience = "identity";

    private readonly string connectionString;
    private readonly string rabbitMqUri;

    private IdentityApp(string connectionString, string rabbitMqUri)
    {
        this.connectionString = connectionString;
        this.rabbitMqUri = rabbitMqUri;
    }

    public static async Task<IdentityApp> StartAsync(ContainersFixture containers, string? connectionString = null)
    {
        ArgumentNullException.ThrowIfNull(containers);
        var database = connectionString ?? await containers.Postgres.CreateDatabaseAsync(TestContext.Current.CancellationToken);
        var app = new IdentityApp(database, containers.RabbitMq.ConnectionString);
        _ = app.Server;
        return app;
    }

    public async Task WaitUntilMigratedAsync()
    {
        var state = Services.GetRequiredService<MigrationState>();
        var deadline = DateTimeOffset.UtcNow.AddSeconds(60);
        while (!state.Applied && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(100, TestContext.Current.CancellationToken);
        }

        Assert.True(state.Applied, "Identity migrations were not applied within 60 seconds.");
    }

    public async Task AssignAsync(string tenantId, Guid userId, string role)
    {
        await WaitUntilMigratedAsync();
        await using var scope = Services.CreateAsyncScope();
        var tenant = scope.ServiceProvider.GetRequiredService<TenantContext>();
        tenant.Set(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        db.RoleAssignments.Add(RoleAssignment.Create(userId, role, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.UseSetting("ConnectionStrings:Identity", connectionString);
        builder.UseSetting("RabbitMq:Uri", rabbitMqUri);
        builder.UseSetting("Authentication:Authority", TestTokens.Issuer);
        builder.UseSetting("Authentication:Audience", Audience);
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureTestServices(services => services.AddTestTokenValidation(Audience));
    }
}
