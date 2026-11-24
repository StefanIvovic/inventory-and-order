using Identity.Tests.Flow;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

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

    private static async Task<string?> ConcurrencyTokenAsync(IdentityApp app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var client = (await manager.FindByClientIdAsync(IdentityApp.Client, TestContext.Current.CancellationToken))!;
        return await manager.GetPropertiesAsync(client, TestContext.Current.CancellationToken) is { } _
            ? (client as OpenIddict.EntityFrameworkCore.Models.OpenIddictEntityFrameworkCoreApplication<Guid>)?.ConcurrencyToken
            : null;
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
