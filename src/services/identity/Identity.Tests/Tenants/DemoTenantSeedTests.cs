using Identity.Api.Protocol;
using Identity.Api.Tenants;
using Identity.Contracts;
using Identity.Tests.Flow;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine.Tracking;

namespace Identity.Tests.Tenants;

public sealed class DemoTenantSeedTests(ContainersFixture containers)
{
    private static readonly Dictionary<string, string?> Demo = new(StringComparer.Ordinal)
    {
        ["Identity:DemoTenant:Slug"] = "demo",
        ["Identity:DemoTenant:Name"] = "Demo Shop",
        ["Identity:DemoTenant:AdminEmail"] = "admin@demo.test",
        ["Identity:DemoTenant:AdminPassword"] = "Demo#Pass1",
    };

    [Fact]
    public async Task StartupCreatesTheDemoTenantOnceAndItsAdminSignsIn()
    {
        var database = await containers.Postgres.CreateDatabaseAsync(TestContext.Current.CancellationToken);
        await using (var first = await IdentityApp.StartAsync(containers, database, Demo))
        {
            await first.WaitUntilMigratedAsync();
            await CodeFlow.SignInAsync(first, "admin@demo.test", "Demo#Pass1");
        }

        await using var second = await IdentityApp.StartAsync(containers, database, Demo);
        await second.WaitUntilMigratedAsync();

        Assert.Equal(1, await CreateTenantTests.CountAsync(second, db => db.Tenants.CountAsync(t => t.Slug == "demo", TestContext.Current.CancellationToken)));
        Assert.Equal(1, await CreateTenantTests.CountAsync(second, db => db.Users.CountAsync(u => u.Email == "admin@demo.test", TestContext.Current.CancellationToken)));
    }

    [Fact]
    public async Task WithoutSettingsNoDemoTenantIsCreated()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        await app.WaitUntilMigratedAsync();

        Assert.Equal(0, await CreateTenantTests.CountAsync(app, db => db.Tenants.CountAsync(TestContext.Current.CancellationToken)));
    }

    [Fact]
    public async Task SeedingPublishesTheEventsThroughTheOutboxAndOnlyOnce()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        await app.WaitUntilMigratedAsync();
        var settings = new DemoTenantSettings { Slug = "demo", Name = "Demo Shop", AdminEmail = "admin@demo.test", AdminPassword = "Demo#Pass1" };

        var first = await app.TrackAsync(() => DemoTenantSeeder.SeedAsync(app.Services, settings, TestContext.Current.CancellationToken));
        var second = await app.TrackAsync(() => DemoTenantSeeder.SeedAsync(app.Services, settings, TestContext.Current.CancellationToken));

        var created = Assert.Single(first.Sent.Envelopes(), e => e.Message is TenantCreated);
        Assert.Single(first.Sent.MessagesOf<UserRegistered>());
        Assert.Equal(((TenantCreated)created.Message!).TenantId, created.TenantId);
        Assert.Empty(second.Sent.MessagesOf<TenantCreated>());
    }
}
