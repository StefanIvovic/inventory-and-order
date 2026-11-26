using BuildingBlocks.Core.Tenancy;
using BuildingBlocks.Testing;
using Identity.Api.Persistence;
using Identity.Api.Users;
using Identity.Tests.Flow;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Identity.Tests;

public sealed class PlatformAdminSeedTests(ContainersFixture containers)
{
    [Fact]
    public async Task EmptyDatabaseGetsAPlatformAdminWhoCanSignIn()
    {
        await using var app = await IdentityApp.StartAsync(containers);

        var tokens = await CodeFlow.SignInAsync(app);

        Assert.NotNull(tokens.AccessToken);
        await app.WithUsersAsync(async users =>
        {
            var admin = (await users.FindByNameAsync(IdentityApp.AdminEmail))!;
            Assert.Equal(UserTypes.Platform, admin.UserType);
            Assert.Null(admin.TenantId);
        });
    }

    [Fact]
    public async Task SeededPlatformAdminHoldsThePlatformRole()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        using var client = app.CreateClient();

        var response = await client.GetAsync("/tenants", await app.PlatformTokenAsync());

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PlatformUserFromA012DatabaseGetsThePlatformRoleOnUpgrade()
    {
        var database = await containers.Postgres.CreateDatabaseAsync(TestContext.Current.CancellationToken);
        var userId = Guid.NewGuid();
        var builder = new DbContextOptionsBuilder<IdentityDbContext>().UseNpgsql(database);
        builder.UseOpenIddict<Guid>();
        var options = builder.Options;
        await using (var db = new IdentityDbContext(options, new TenantContext()))
        {
            await db.GetService<IMigrator>().MigrateAsync("20261124152319_Authentication", TestContext.Current.CancellationToken);
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO identity_users (\"Id\", \"UserName\", \"NormalizedUserName\", \"Email\", \"NormalizedEmail\", \"EmailConfirmed\", \"SecurityStamp\", \"ConcurrencyStamp\", \"PhoneNumberConfirmed\", \"TwoFactorEnabled\", \"LockoutEnabled\", \"AccessFailedCount\", \"UserType\") VALUES ({userId}, 'old@platform.test', 'OLD@PLATFORM.TEST', 'old@platform.test', 'OLD@PLATFORM.TEST', true, 'stamp', 'concurrency', false, false, true, 0, 'platform')",
                TestContext.Current.CancellationToken);
        }

        await using var app = await IdentityApp.StartAsync(containers, database);
        await app.WaitUntilMigratedAsync();
        using var client = app.CreateClient();

        var response = await client.GetAsync("/tenants", TestTokens.Platform(userId, IdentityApp.Audience));

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        await app.WithUsersAsync(async users => Assert.Null(await users.FindByNameAsync(IdentityApp.AdminEmail)));
    }

    [Theory]
    [InlineData(null, IdentityApp.AdminPassword, "Identity:PlatformAdmin:Email")]
    [InlineData(IdentityApp.AdminEmail, null, "Identity:PlatformAdmin:Password")]
    [InlineData(IdentityApp.AdminEmail, "short", "could not be created")]
    public async Task MissingOrInvalidSettingsStopStartup(string? email, string? password, string expected)
    {
        var overrides = new Dictionary<string, string?>
        {
            ["Identity:PlatformAdmin:Email"] = email,
            ["Identity:PlatformAdmin:Password"] = password,
        };

        var error = await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await using var app = await IdentityApp.StartAsync(containers, overrides: overrides);
        });

        Assert.Contains(expected, error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExistingAdminIsNeverChanged()
    {
        var database = await containers.Postgres.CreateDatabaseAsync(TestContext.Current.CancellationToken);
        string? stamp;
        await using (var first = await IdentityApp.StartAsync(containers, database))
        {
            await first.WaitUntilMigratedAsync();
            stamp = await StampAsync(first);
        }

        await using var second = await IdentityApp.StartAsync(containers, database, new Dictionary<string, string?>
        {
            ["Identity:PlatformAdmin:Email"] = "other@platform.test",
            ["Identity:PlatformAdmin:Password"] = "Other#Pass1",
        });
        await second.WaitUntilMigratedAsync();

        await second.WithUsersAsync(async users =>
        {
            Assert.Equal(1, await users.Users.CountAsync(u => u.UserTypeId == UserTypes.PlatformValue.Id, TestContext.Current.CancellationToken));
            Assert.Null(await users.FindByNameAsync("other@platform.test"));
        });
        Assert.Equal(stamp, await StampAsync(second));
    }

    private static async Task<string?> StampAsync(IdentityApp app)
    {
        string? stamp = null;
        await app.WithUsersAsync(async users => stamp = (await users.FindByNameAsync(IdentityApp.AdminEmail))!.SecurityStamp);
        return stamp;
    }
}
