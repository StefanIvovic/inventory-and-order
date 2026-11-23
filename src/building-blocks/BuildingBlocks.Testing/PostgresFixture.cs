using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace BuildingBlocks.Testing;

public sealed class PostgresFixture : IAsyncLifetime
{
    public const string ImageKey = "POSTGRES_IMAGE";
    public const ushort ContainerPort = 5432;
    public const string DataPath = "/var/lib/postgresql";

    private readonly PostgreSqlContainer container;

    public PostgresFixture()
    {
        TestInfrastructure.EnsureConfigured();
        container = new PostgreSqlBuilder(ImageVersions.Get(ImageKey))
            .WithTmpfsMount(DataPath)
            .Build();
    }

    public string ConnectionString => container.GetConnectionString();

    public ushort HostPort => container.GetMappedPublicPort(ContainerPort);

    public string ContainerId => container.Id;

    public async Task<string> CreateDatabaseAsync(CancellationToken cancellationToken = default)
    {
        var name = $"t_{Guid.NewGuid():N}";
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "CREATE DATABASE " + new NpgsqlCommandBuilder().QuoteIdentifier(name);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return new NpgsqlConnectionStringBuilder(ConnectionString) { Database = name }.ConnectionString;
    }

    public async ValueTask InitializeAsync() => await container.StartAsync();

    public ValueTask DisposeAsync() => container.DisposeAsync();
}
