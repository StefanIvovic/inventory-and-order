using Npgsql;

namespace BuildingBlocks.Testing.Tests;

public sealed class PostgresFixtureTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task ConnectionStringRunsQuery()
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand("SELECT 1", connection);

        Assert.Equal(1, await command.ExecuteScalarAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ContainerUsesPinnedImage()
    {
        var inspect = await DockerCli.InspectAsync(postgres.ContainerId);

        Assert.Equal(ImageVersions.Get(PostgresFixture.ImageKey), DockerCli.ImageOf(inspect));
    }

    [Fact]
    public void HostPortIsRandomNotDefault()
    {
        Assert.NotEqual(PostgresFixture.ContainerPort, postgres.HostPort);
    }

    [Fact]
    public async Task DataIsInMemoryWithoutVolumes()
    {
        var inspect = await DockerCli.InspectAsync(postgres.ContainerId);

        Assert.Contains(PostgresFixture.DataPath, DockerCli.TmpfsOf(inspect));
        Assert.Empty(DockerCli.VolumeMountsOf(inspect));
    }

    [Fact]
    public async Task CreatedDatabasesAreIsolated()
    {
        var cancellation = TestContext.Current.CancellationToken;
        var first = await postgres.CreateDatabaseAsync(cancellation);
        var second = await postgres.CreateDatabaseAsync(cancellation);

        await Execute(first, "CREATE TABLE only_here (id int)", cancellation);

        Assert.NotEqual(new NpgsqlConnectionStringBuilder(first).Database, new NpgsqlConnectionStringBuilder(second).Database);
        Assert.Equal(1L, await Execute(first, "SELECT count(*) FROM information_schema.tables WHERE table_name = 'only_here'", cancellation));
        Assert.Equal(0L, await Execute(second, "SELECT count(*) FROM information_schema.tables WHERE table_name = 'only_here'", cancellation));
    }

    [Fact]
    public async Task DisposeRemovesContainer()
    {
        var fixture = new PostgresFixture();
        await fixture.InitializeAsync();
        var id = fixture.ContainerId;
        Assert.True(await DockerCli.ExistsAsync(id));

        await fixture.DisposeAsync();

        Assert.False(await DockerCli.ExistsAsync(id));
    }

    private static async Task<object?> Execute(string connectionString, string sql, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        return await command.ExecuteScalarAsync(cancellationToken);
    }
}
