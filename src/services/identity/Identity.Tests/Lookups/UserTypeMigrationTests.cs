using BuildingBlocks.Core.Tenancy;
using Identity.Api.Persistence;
using Identity.Api.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Identity.Tests.Lookups;

public sealed class UserTypeMigrationTests(ContainersFixture containers)
{
    private const string PreviousMigration = "20261125074234_Customers";
    private static readonly Guid PlatformUser = Guid.Parse("a1000000-0000-4000-8000-000000000001");
    private static readonly Guid StaffUser = Guid.Parse("a1000000-0000-4000-8000-000000000002");
    private static readonly Guid CustomerUser = Guid.Parse("a1000000-0000-4000-8000-000000000003");

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ExistingUsersKeepTheirTypeAndEverythingElse()
    {
        var database = await UpgradedFromPreviousAsync();

        var rows = await QueryAsync(
            database,
            "SELECT u.\"Id\", v.\"Code\", u.\"TenantId\", u.\"Email\", u.\"UserName\", u.\"SecurityStamp\", u.\"DisabledAt\" IS NOT NULL"
            + " FROM identity_users u JOIN lookup_values v ON v.\"Id\" = u.\"UserTypeId\" ORDER BY u.\"Id\"");

        Assert.Equal(
            [
                $"{PlatformUser}|platform||admin@platform.test|admin@platform.test|stamp-1|False",
                $"{StaffUser}|staff|acme|clerk@acme.test|clerk@acme.test|stamp-2|True",
                $"{CustomerUser}|customer|acme|pat@mail.test|acme:pat@mail.test|stamp-3|False",
            ],
            rows);
    }

    [Fact]
    public async Task UserTypeColumnIsGone()
    {
        var database = await UpgradedFromPreviousAsync();

        var columns = await QueryAsync(
            database,
            "SELECT column_name FROM information_schema.columns WHERE table_name = 'identity_users' AND column_name LIKE 'UserType%'");

        Assert.Equal(["UserTypeId"], columns);
    }

    [Fact]
    public async Task DatabaseRefusesAnIdThatIsNotAUserType()
    {
        var database = await UpgradedFromPreviousAsync();

        foreach (var id in new[] { StaffStatuses.Type.Values[0].Id, Guid.NewGuid() })
        {
            var error = await Assert.ThrowsAsync<PostgresException>(() =>
                ExecuteAsync(database, $"UPDATE identity_users SET \"UserTypeId\" = '{id}' WHERE \"Id\" = '{StaffUser}'"));
            Assert.Contains(error.SqlState, new[] { PostgresErrorCodes.CheckViolation, PostgresErrorCodes.ForeignKeyViolation });
        }
    }

    [Fact]
    public async Task TenantRuleStillHolds()
    {
        var database = await UpgradedFromPreviousAsync();

        var error = await Assert.ThrowsAsync<PostgresException>(() =>
            ExecuteAsync(database, $"UPDATE identity_users SET \"TenantId\" = NULL WHERE \"Id\" = '{StaffUser}'"));

        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        Assert.Equal("ck_identity_users_type_tenant", error.ConstraintName);
    }

    [Fact]
    public async Task RollingBackRestoresTheTextColumn()
    {
        var database = await UpgradedFromPreviousAsync();
        await using (var context = Context(database))
        {
            await context.GetService<IMigrator>().MigrateAsync(PreviousMigration, Cancellation);
        }

        var rows = await QueryAsync(database, "SELECT \"UserType\" FROM identity_users ORDER BY \"Id\"");

        Assert.Equal(["platform", "staff", "customer"], rows);
        Assert.Empty(await QueryAsync(database, "SELECT 1 WHERE to_regclass('public.lookup_values') IS NOT NULL"));
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
            Insert(PlatformUser, "platform", null, "admin@platform.test", "admin@platform.test", "stamp-1", disabled: false)
            + Insert(StaffUser, "staff", "acme", "clerk@acme.test", "clerk@acme.test", "stamp-2", disabled: true)
            + Insert(CustomerUser, "customer", "acme", "pat@mail.test", "acme:pat@mail.test", "stamp-3", disabled: false));

        await using (var context = Context(database))
        {
            await context.Database.MigrateAsync(Cancellation);
        }

        return database;
    }

    private static string Insert(Guid id, string type, string? tenant, string email, string userName, string stamp, bool disabled) =>
        "INSERT INTO identity_users (\"Id\", \"UserType\", \"TenantId\", \"CreatedAt\", \"DisabledAt\", \"UserName\", \"NormalizedUserName\","
        + " \"Email\", \"NormalizedEmail\", \"EmailConfirmed\", \"SecurityStamp\", \"ConcurrencyStamp\", \"PhoneNumberConfirmed\","
        + " \"TwoFactorEnabled\", \"LockoutEnabled\", \"AccessFailedCount\") VALUES ("
        + $"'{id}', '{type}', {(tenant is null ? "NULL" : $"'{tenant}'")}, now(), {(disabled ? "now()" : "NULL")}, '{userName}', '{userName.ToUpperInvariant()}',"
        + $" '{email}', '{email.ToUpperInvariant()}', true, '{stamp}', '{Guid.NewGuid()}', false, false, true, 0);";

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
