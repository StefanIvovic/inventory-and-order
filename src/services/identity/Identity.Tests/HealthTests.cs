using System.Net;
using Npgsql;

namespace Identity.Tests;

public sealed class HealthTests(ContainersFixture containers)
{
    [Fact]
    public async Task LiveAndReadyAnswerWithoutTokenOnceMigrated()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        using var client = app.CreateClient();
        await app.WaitUntilMigratedAsync();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready", TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task MigrationsCreateRoleAssignmentsTable()
    {
        var database = await containers.Postgres.CreateDatabaseAsync(TestContext.Current.CancellationToken);
        await using var app = await IdentityApp.StartAsync(containers, database);
        await app.WaitUntilMigratedAsync();

        await using var connection = new NpgsqlConnection(database);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand("SELECT to_regclass('public.role_assignments') IS NOT NULL", connection);

        Assert.True((bool)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!);
    }

    [Fact]
    public async Task ReadyIs503AndLiveIs200WhenDatabaseGoesAway()
    {
        var database = await containers.Postgres.CreateDatabaseAsync(TestContext.Current.CancellationToken);
        await using var app = await IdentityApp.StartAsync(containers, database);
        using var client = app.CreateClient();
        await app.WaitUntilMigratedAsync();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready", TestContext.Current.CancellationToken)).StatusCode);

        var name = new NpgsqlConnectionStringBuilder(database).Database!;
        await using (var admin = new NpgsqlConnection(containers.Postgres.ConnectionString))
        {
            await admin.OpenAsync(TestContext.Current.CancellationToken);
            await using var terminate = new NpgsqlCommand(
                $"ALTER DATABASE \"{name}\" WITH ALLOW_CONNECTIONS false; SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '{name}'",
                admin);
            await terminate.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        NpgsqlConnection.ClearAllPools();
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/health/ready", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live", TestContext.Current.CancellationToken)).StatusCode);
    }
}
