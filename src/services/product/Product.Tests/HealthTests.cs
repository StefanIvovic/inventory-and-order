using System.Net;
using Npgsql;

namespace Product.Tests;

public sealed class HealthTests(ContainersFixture containers)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task LiveAndReadyAnswerWithoutTokenOnceMigrated()
    {
        await using var app = await ProductApp.StartAsync(containers);
        using var client = app.CreateClient();
        await app.WaitUntilMigratedAsync();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live", Cancellation)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready", Cancellation)).StatusCode);
    }

    [Fact]
    public async Task MigrationsCreateTheRoleTableAndTheOutboxAndInbox()
    {
        await using var app = await ProductApp.StartAsync(containers);
        await app.WaitUntilMigratedAsync();

        Assert.True(await ExistsAsync(app.Database, "public.role_assignments"));
        Assert.True(await ExistsAsync(app.Database, "wolverine.wolverine_incoming_envelopes"));
        Assert.True(await ExistsAsync(app.Database, "wolverine.wolverine_outgoing_envelopes"));
    }

    [Fact]
    public async Task TwoInstancesOnOneFreshDatabaseBothBecomeReady()
    {
        var database = await containers.Postgres.CreateDatabaseAsync(Cancellation);
        var first = ProductApp.StartAsync(containers, database);
        var second = ProductApp.StartAsync(containers, database);
        await using var one = await first;
        await using var two = await second;

        await Task.WhenAll(one.WaitUntilMigratedAsync(), two.WaitUntilMigratedAsync());

        using var client = two.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready", Cancellation)).StatusCode);
        Assert.Equal(1L, await CountAsync(database, "SELECT count(*) FROM \"__EFMigrationsHistory\""));
    }

    [Fact]
    public async Task ReadyIs503AndLiveIs200WhenDatabaseGoesAway()
    {
        var database = await containers.Postgres.CreateDatabaseAsync(Cancellation);
        await using var app = await ProductApp.StartAsync(containers, database);
        using var client = app.CreateClient();
        await app.WaitUntilMigratedAsync();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready", Cancellation)).StatusCode);

        var name = new NpgsqlConnectionStringBuilder(database).Database!;
        await using (var admin = new NpgsqlConnection(containers.Postgres.ConnectionString))
        {
            await admin.OpenAsync(Cancellation);
            await using var terminate = new NpgsqlCommand(
                $"ALTER DATABASE \"{name}\" WITH ALLOW_CONNECTIONS false; SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '{name}'",
                admin);
            await terminate.ExecuteNonQueryAsync(Cancellation);
        }

        NpgsqlConnection.ClearAllPools();
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/health/ready", Cancellation)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live", Cancellation)).StatusCode);
    }

    private static async Task<bool> ExistsAsync(string database, string table) =>
        await CountAsync(database, $"SELECT count(*) FROM (SELECT to_regclass('{table}') AS t) x WHERE t IS NOT NULL") == 1;

    private static async Task<long> CountAsync(string database, string sql)
    {
        await using var connection = new NpgsqlConnection(database);
        await connection.OpenAsync(Cancellation);
        await using var command = new NpgsqlCommand(sql, connection);
        return (long)(await command.ExecuteScalarAsync(Cancellation))!;
    }
}
