using BuildingBlocks.Core.Tenancy;
using Identity.Api.Lookups;
using Identity.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Identity.Tests.Lookups;

public sealed class LookupSeedingTests(ContainersFixture containers)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static IReadOnlyList<string> Expected =>
    [
        .. IdentityLookups.Catalog.Types.Select(t => $"{t.Id}|{t.Code}|{t.Name}|"),
        .. IdentityLookups.Catalog.Types.SelectMany(t => t.Values.Select(v => $"{v.Id}|{v.Code}|{v.Name}|{t.Id}")),
    ];

    [Fact]
    public async Task FreshStartHasEveryDefinedLookupWithItsId()
    {
        var database = await containers.Postgres.CreateDatabaseAsync(Cancellation);

        await StartAndStopAsync(database);

        Assert.Equal(Expected.Order(StringComparer.Ordinal), await RowsAsync(database));
        Assert.Equal(7, Expected.Count);
    }

    [Fact]
    public async Task NameEditedByHandIsRestoredOnTheNextStart()
    {
        var database = await containers.Postgres.CreateDatabaseAsync(Cancellation);
        await StartAndStopAsync(database);
        await ExecuteAsync(database, "UPDATE lookup_values SET \"Name\" = 'Employee' WHERE \"Code\" = 'staff'");

        await StartAndStopAsync(database);

        Assert.Equal(Expected.Order(StringComparer.Ordinal), await RowsAsync(database));
    }

    [Fact]
    public async Task SecondStartChangesNothing()
    {
        var database = await containers.Postgres.CreateDatabaseAsync(Cancellation);
        await StartAndStopAsync(database);
        await ExecuteAsync(
            database,
            "CREATE TABLE lookup_writes (n int); CREATE FUNCTION count_write() RETURNS trigger AS $$ BEGIN INSERT INTO lookup_writes VALUES (1); RETURN NULL; END $$ LANGUAGE plpgsql;"
            + " CREATE TRIGGER count_types AFTER INSERT OR UPDATE OR DELETE ON lookup_types FOR EACH ROW EXECUTE FUNCTION count_write();"
            + " CREATE TRIGGER count_values AFTER INSERT OR UPDATE OR DELETE ON lookup_values FOR EACH ROW EXECUTE FUNCTION count_write();");

        await StartAndStopAsync(database);

        Assert.Equal(["0"], await QueryAsync(database, "SELECT count(*) FROM lookup_writes"));
    }

    [Fact]
    public async Task UpgradedDatabaseGetsTheStaffStatusesAndKeepsTheUserTypeIds()
    {
        var database = await containers.Postgres.CreateDatabaseAsync(Cancellation);
        await using (var context = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>().UseNpgsql(database).Options, new TenantContext()))
        {
            await context.Database.MigrateAsync(Cancellation);
        }

        Assert.Equal(4, (await RowsAsync(database)).Count);

        await StartAndStopAsync(database);

        Assert.Equal(Expected.Order(StringComparer.Ordinal), await RowsAsync(database));
    }

    private async Task StartAndStopAsync(string database)
    {
        await using var app = await IdentityApp.StartAsync(containers, database);
        await app.WaitUntilMigratedAsync();
    }

    private static Task<List<string>> RowsAsync(string database) => QueryAsync(
        database,
        "SELECT row FROM (SELECT \"Id\" || '|' || \"Code\" || '|' || \"Name\" || '|' AS row FROM lookup_types"
        + " UNION ALL SELECT \"Id\" || '|' || \"Code\" || '|' || \"Name\" || '|' || \"LookupTypeId\" FROM lookup_values) rows ORDER BY row COLLATE \"C\"");

    private static async Task ExecuteAsync(string database, string sql)
    {
        await using var connection = new NpgsqlConnection(database);
        await connection.OpenAsync(Cancellation);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(Cancellation);
    }

    private static async Task<List<string>> QueryAsync(string database, string sql)
    {
        await using var connection = new NpgsqlConnection(database);
        await connection.OpenAsync(Cancellation);
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(Cancellation);
        var rows = new List<string>();
        while (await reader.ReadAsync(Cancellation))
        {
            rows.Add(Convert.ToString(reader.GetValue(0), System.Globalization.CultureInfo.InvariantCulture)!);
        }

        return rows;
    }
}
