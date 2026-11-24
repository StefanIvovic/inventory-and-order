using Identity.Tests.Flow;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;

namespace Identity.Tests;

public sealed class ClientRegistrarTests(ContainersFixture containers)
{
    [Fact]
    public async Task RestartWithSameSettingsChangesNothing()
    {
        var database = await containers.Postgres.CreateDatabaseAsync(TestContext.Current.CancellationToken);
        string? before;
        await using (var first = await IdentityApp.StartAsync(containers, database))
        {
            await first.WaitUntilMigratedAsync();
            before = await ConcurrencyTokenAsync(first);
        }

        await using var second = await IdentityApp.StartAsync(containers, database);
        await second.WaitUntilMigratedAsync();

        Assert.Equal(before, await ConcurrencyTokenAsync(second));
        Assert.Equal(1, await CountAsync(second));
    }

    [Fact]
    public async Task ChangedSecretAndRedirectAreUpdatedInPlace()
    {
        var database = await containers.Postgres.CreateDatabaseAsync(TestContext.Current.CancellationToken);
        await using (var first = await IdentityApp.StartAsync(containers, database))
        {
            await first.WaitUntilMigratedAsync();
        }

        await using var second = await IdentityApp.StartAsync(containers, database, new Dictionary<string, string?>
        {
            ["Identity:Clients:backoffice-bff:Secret"] = "rotated-secret",
            ["Identity:Clients:backoffice-bff:RedirectUri"] = "https://gateway.test/bff/backoffice/callback-v2",
        });
        await second.WaitUntilMigratedAsync();

        await using var scope = second.Services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var client = (await manager.FindByClientIdAsync(IdentityApp.Client, TestContext.Current.CancellationToken))!;
        Assert.True(await manager.ValidateClientSecretAsync(client, "rotated-secret", TestContext.Current.CancellationToken));
        Assert.False(await manager.ValidateClientSecretAsync(client, IdentityApp.ClientSecret, TestContext.Current.CancellationToken));
        Assert.Equal(["https://gateway.test/bff/backoffice/callback-v2"], await manager.GetRedirectUrisAsync(client, TestContext.Current.CancellationToken));
        Assert.Equal(1, await CountAsync(second));
    }

    [Fact]
    public async Task RemovedPermissionAndChangedScopeResourceAreRestored()
    {
        var database = await containers.Postgres.CreateDatabaseAsync(TestContext.Current.CancellationToken);
        await using (var first = await IdentityApp.StartAsync(containers, database))
        {
            await first.WaitUntilMigratedAsync();
            await using var scope = first.Services.CreateAsyncScope();
            var applications = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
            var scopes = scope.ServiceProvider.GetRequiredService<IOpenIddictScopeManager>();
            var client = (await applications.FindByClientIdAsync(IdentityApp.Client, TestContext.Current.CancellationToken))!;
            var descriptor = new OpenIddictApplicationDescriptor();
            await applications.PopulateAsync(descriptor, client, TestContext.Current.CancellationToken);
            descriptor.Permissions.Remove(OpenIddictConstants.Permissions.GrantTypes.RefreshToken);
            descriptor.ClientSecret = IdentityApp.ClientSecret;
            await applications.UpdateAsync(client, descriptor, TestContext.Current.CancellationToken);
            var order = (await scopes.FindByNameAsync("order", TestContext.Current.CancellationToken))!;
            await scopes.UpdateAsync(order, new OpenIddictScopeDescriptor { Name = "order", Resources = { "elsewhere" } }, TestContext.Current.CancellationToken);
        }

        await using var second = await IdentityApp.StartAsync(containers, database);
        await second.WaitUntilMigratedAsync();

        await using var check = second.Services.CreateAsyncScope();
        var manager = check.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var scopeManager = check.ServiceProvider.GetRequiredService<IOpenIddictScopeManager>();
        var restored = (await manager.FindByClientIdAsync(IdentityApp.Client, TestContext.Current.CancellationToken))!;
        var order2 = (await scopeManager.FindByNameAsync("order", TestContext.Current.CancellationToken))!;
        Assert.True(await manager.HasPermissionAsync(restored, OpenIddictConstants.Permissions.GrantTypes.RefreshToken, TestContext.Current.CancellationToken));
        Assert.True(await manager.ValidateClientSecretAsync(restored, IdentityApp.ClientSecret, TestContext.Current.CancellationToken));
        Assert.Equal(["order"], await scopeManager.GetResourcesAsync(order2, TestContext.Current.CancellationToken));
    }

    private static async Task<string?> ConcurrencyTokenAsync(IdentityApp app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var client = (await manager.FindByClientIdAsync(IdentityApp.Client, TestContext.Current.CancellationToken))!;
        return ((OpenIddictEntityFrameworkCoreApplication<Guid>)client).ConcurrencyToken;
    }

    private static async Task<int> CountAsync(IdentityApp app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var count = 0;
        await foreach (var application in manager.ListAsync(cancellationToken: TestContext.Current.CancellationToken))
        {
            if (await manager.GetClientIdAsync(application, TestContext.Current.CancellationToken) == IdentityApp.Client)
            {
                count++;
            }
        }

        return count;
    }
}
