using BuildingBlocks.Core.Tenancy;
using Identity.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Identity.Tests.Tenants;

public sealed class TenantListMigrationTests(ContainersFixture containers)
{
    private const string PreviousMigration = "20261126074159_StaffListIndexes";

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ExistingTenantsGetTheirUpperCasedName()
    {
        var database = await UpgradedFromPreviousAsync();

        var rows = await QueryAsync(database, "SELECT \"Slug\", \"Name\", \"NormalizedName\" FROM tenants ORDER BY \"Slug\"");

        Assert.Equal(["acme|Acme ltd|ACME LTD", "globex|Globex Holdings|GLOBEX HOLDINGS"], rows);
    }

    [Fact]
    public async Task SlugAndNormalizedNameUseByteOrderAndTheNameIsRequired()
    {
        var database = await UpgradedFromPreviousAsync();

        var columns = await QueryAsync(
            database,
            "SELECT column_name, collation_name, is_nullable FROM information_schema.columns"
            + " WHERE table_name = 'tenants' AND column_name IN ('Slug', 'NormalizedName') ORDER BY column_name");
        var indexes = await QueryAsync(database, "SELECT indexname FROM pg_indexes WHERE tablename = 'tenants' ORDER BY indexname");

        Assert.Equal(["NormalizedName|C|NO", "Slug|C|NO"], columns);
        Assert.Contains("ix_tenants_normalized_name", indexes);
        Assert.Contains("ux_tenants_slug", indexes);
    }

    [Fact]
    public async Task RollingBackDropsTheNameAndRestoresTheSlugCollation()
    {
        var database = await UpgradedFromPreviousAsync();
        await using (var context = Context(database))
        {
            await context.GetService<IMigrator>().MigrateAsync(PreviousMigration, Cancellation);
        }

        var columns = await QueryAsync(
            database,
            "SELECT column_name, coalesce(collation_name, '') FROM information_schema.columns"
            + " WHERE table_name = 'tenants' AND column_name IN ('Slug', 'NormalizedName') ORDER BY column_name");

        Assert.Equal(["Slug|"], columns);
        Assert.Equal(["acme", "globex"], await QueryAsync(database, "SELECT \"Slug\" FROM tenants ORDER BY \"Slug\""));
    }

    private async Task<string> UpgradedFromPreviousAsync()
    {
        var database = await containers.Postgres.CreateDatabaseAsync(Cancellation);
        await using (var context = Context(database))
        {
            await context.GetService<IMigrator>().MigrateAsync(PreviousMigration, Cancellation);
        }

        await ExecuteAsync(
            database,
            "INSERT INTO tenants (\"Id\", \"Slug\", \"Name\", \"CreatedAt\") VALUES"
            + $" ('{Guid.NewGuid():N}', 'acme', 'Acme ltd', now()), ('{Guid.NewGuid():N}', 'globex', 'Globex Holdings', now());");

        await using (var context = Context(database))
        {
            await context.Database.MigrateAsync(Cancellation);
        }

        return database;
    }

    private static IdentityDbContext Context(string database) =>
        new(new DbContextOptionsBuilder<IdentityDbContext>().UseNpgsql(database).Options, new TenantContext());

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
            rows.Add(string.Join('|', Enumerable.Range(0, reader.FieldCount).Select(i => reader.IsDBNull(i) ? string.Empty : Convert.ToString(reader.GetValue(i), System.Globalization.CultureInfo.InvariantCulture))));
        }

        return rows;
    }
}
