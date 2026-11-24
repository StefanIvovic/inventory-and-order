using System.Net;

namespace Identity.Tests.Flow;

public sealed class RefreshTests(ContainersFixture containers)
{
    [Fact]
    public async Task RefreshRotatesAndOldTokenStopsWorking()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        var first = await CodeFlow.SignInAsync(app);
        using var client = app.CreateClient();

        var second = await CodeFlow.RefreshAsync(client, first.RefreshToken!);
        var reused = await CodeFlow.RefreshAsync(client, first.RefreshToken!);

        Assert.Equal(HttpStatusCode.OK, second.Status);
        Assert.NotEqual(first.RefreshToken, second.RefreshToken);
        Assert.NotEqual(first.AccessToken, second.AccessToken);
        Assert.Equal("invalid_grant", reused.Error);
    }

    [Fact]
    public async Task ReuseRevokesTheWholeChain()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        var first = await CodeFlow.SignInAsync(app);
        using var client = app.CreateClient();
        var second = await CodeFlow.RefreshAsync(client, first.RefreshToken!);

        await CodeFlow.RefreshAsync(client, first.RefreshToken!);
        var afterReuse = await CodeFlow.RefreshAsync(client, second.RefreshToken!);

        Assert.Equal("invalid_grant", afterReuse.Error);
    }

    [Fact]
    public async Task SecurityStampChangeBlocksRefresh()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        var tokens = await CodeFlow.SignInAsync(app);
        await app.WithUsersAsync(async users =>
            await users.UpdateSecurityStampAsync((await users.FindByEmailAsync(IdentityApp.AdminEmail))!));
        using var client = app.CreateClient();

        var refreshed = await CodeFlow.RefreshAsync(client, tokens.RefreshToken!);

        Assert.Equal("invalid_grant", refreshed.Error);
    }

    [Fact]
    public async Task LockedOutUserCannotRefresh()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        var tokens = await CodeFlow.SignInAsync(app);
        await app.WithUsersAsync(async users =>
            await users.SetLockoutEndDateAsync((await users.FindByEmailAsync(IdentityApp.AdminEmail))!, DateTimeOffset.UtcNow.AddMinutes(10)));
        using var client = app.CreateClient();

        var refreshed = await CodeFlow.RefreshAsync(client, tokens.RefreshToken!);

        Assert.Equal("invalid_grant", refreshed.Error);
    }

    [Fact]
    public async Task ConcurrentRefreshSucceedsAtMostOnce()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        var tokens = await CodeFlow.SignInAsync(app);
        using var client = app.CreateClient();

        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => CodeFlow.RefreshAsync(client, tokens.RefreshToken!)));

        Assert.True(results.Count(r => r.Status == HttpStatusCode.OK) <= 1);
    }
}
