using Identity.Api.Users;
using Identity.Tests.Flow;
using Microsoft.EntityFrameworkCore;

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
            var admin = (await users.FindByEmailAsync(IdentityApp.AdminEmail))!;
            Assert.Equal(UserTypes.Platform, admin.UserType);
            Assert.Null(admin.TenantId);
        });
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
            Assert.Equal(1, await users.Users.CountAsync(u => u.UserType == UserTypes.Platform, TestContext.Current.CancellationToken));
            Assert.Null(await users.FindByEmailAsync("other@platform.test"));
        });
        Assert.Equal(stamp, await StampAsync(second));
    }

    private static async Task<string?> StampAsync(IdentityApp app)
    {
        string? stamp = null;
        await app.WithUsersAsync(async users => stamp = (await users.FindByEmailAsync(IdentityApp.AdminEmail))!.SecurityStamp);
        return stamp;
    }
}
