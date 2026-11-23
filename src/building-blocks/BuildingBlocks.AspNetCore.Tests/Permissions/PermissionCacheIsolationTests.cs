using System.Net;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using static BuildingBlocks.AspNetCore.Tests.TestApp;

namespace BuildingBlocks.AspNetCore.Tests.Permissions;

public sealed class PermissionCacheIsolationTests
{
    private static readonly Guid UserId = Guid.Parse("6f9619ff-8b86-d011-b42d-00cf4fc964ff");

    [Fact]
    public async Task SizeLimitedSharedCacheDoesNotAffectPermissions()
    {
        await using var app = await StartAsync();
        app.Permissions.Grant(UserId, "acme", "inventory.adjust");
        var claims = TestAuthHandler.Claims(UserId.ToString(), "acme", "staff");

        var first = await app.Client.SendAsync(Get("/perm/ensure?p=inventory.adjust", claims), TestContext.Current.CancellationToken);
        var second = await app.Client.SendAsync(Get("/perm/ensure?p=inventory.adjust", claims), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(1, app.Permissions.Calls);
    }

    [Fact]
    public async Task SharedCacheStaysEmpty()
    {
        await using var app = await StartAsync();
        var claims = TestAuthHandler.Claims(UserId.ToString(), "acme", "staff");

        await app.Client.SendAsync(Get("/perm/granted", claims), TestContext.Current.CancellationToken);

        var shared = (MemoryCache)app.Services.GetRequiredService<IMemoryCache>();
        Assert.Equal(0, shared.Count);
    }
}
