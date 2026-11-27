using BuildingBlocks.Testing;
using JasperFx.CommandLine;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Product.Api.Persistence;

[assembly: AssemblyFixture(typeof(Product.Tests.ContainersFixture))]

namespace Product.Tests;

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

public sealed class ProductApp : WebApplicationFactory<Program>
{
    public const string Audience = "product";

    private readonly Dictionary<string, string?> settings;

    private ProductApp(Dictionary<string, string?> settings)
    {
        this.settings = settings;
    }

    public string Database => settings["ConnectionStrings:Product"]!;

    public static async Task<ProductApp> StartAsync(ContainersFixture containers, string? connectionString = null)
    {
        ArgumentNullException.ThrowIfNull(containers);
        var database = connectionString ?? await containers.Postgres.CreateDatabaseAsync(TestContext.Current.CancellationToken);
        var app = new ProductApp(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["ConnectionStrings:Product"] = database,
            ["RabbitMq:Uri"] = containers.RabbitMq.ConnectionString,
            ["Authentication:Authority"] = TestTokens.Issuer,
            ["Authentication:Audience"] = Audience,
            ["Authentication:RequireHttpsMetadata"] = "false",
        });
        _ = app.Server;
        return app;
    }

    public static string StaffToken(Guid userId, string tenantId) => TestTokens.For(userId, tenantId, audience: Audience);

    public async Task WaitUntilMigratedAsync()
    {
        var state = Services.GetRequiredService<MigrationState>();
        var deadline = DateTimeOffset.UtcNow.AddSeconds(60);
        while (!state.Applied && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(100, TestContext.Current.CancellationToken);
        }

        Assert.True(state.Applied, "Product migrations did not finish within 60 seconds.");
    }

    public async Task AssignAsync(string tenantId, Guid userId, string role)
    {
        await using var connection = new NpgsqlConnection(Database);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(
            "INSERT INTO role_assignments (\"TenantId\", \"UserId\", \"Role\", \"AssignedAt\") VALUES (@tenant, @user, @role, now())",
            connection);
        command.Parameters.AddWithValue("tenant", tenantId);
        command.Parameters.AddWithValue("user", userId);
        command.Parameters.AddWithValue("role", role);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        using var connection = new NpgsqlConnection(Database);
        NpgsqlConnection.ClearPool(connection);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        foreach (var (key, value) in settings)
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureTestServices(services => services.AddTestTokenValidation(Audience));
    }
}
