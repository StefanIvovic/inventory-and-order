# Contract: Testing API

```csharp
namespace BuildingBlocks.Testing;
public static class ImageVersions
{
    public const string PathVariable = "IAO_VERSIONS_FILE";
    public static string Path { get; }
    public static string Get(string key);
}
public static class TestInfrastructure { public static void EnsureConfigured(); }
public sealed class PostgresFixture : IAsyncLifetime
{
    public string ConnectionString { get; }
    public ushort HostPort { get; }
    public string ContainerId { get; }
    public Task<string> CreateDatabaseAsync(CancellationToken cancellationToken = default);
}
public sealed class RabbitMqFixture : IAsyncLifetime
{
    public string ConnectionString { get; }
    public ushort HostPort { get; }
    public string ContainerId { get; }
}
```

## Usage (SC-003)

```csharp
public sealed class OrderTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task Works()
    {
        var connectionString = await postgres.CreateDatabaseAsync(TestContext.Current.CancellationToken);
    }
}
```

## Script

```text
pwsh scripts/check-test-cleanup.ps1 [-TimeoutSeconds 60]
```

Exit codes are in [data-model.md](../data-model.md).
