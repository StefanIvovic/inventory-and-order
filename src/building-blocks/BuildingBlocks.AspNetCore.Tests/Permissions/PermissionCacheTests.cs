using System.Net;
using static BuildingBlocks.AspNetCore.Tests.TestApp;

namespace BuildingBlocks.AspNetCore.Tests.Permissions;

public sealed class PermissionCacheTests
{
    private static readonly Guid UserId = Guid.Parse("6f9619ff-8b86-d011-b42d-00cf4fc964ff");
    private static readonly Guid OtherUserId = Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e");

    [Fact]
    public async Task SeveralChecksInOneRequestCallProviderOnce()
    {
        await using var app = await StartAsync();

        await Send(app, "/perm/repeat", UserId, "acme");

        Assert.Equal(1, app.Permissions.Calls);
    }

    [Fact]
    public async Task SecondRequestWithinDurationUsesCache()
    {
        await using var app = await StartAsync();

        await Send(app, "/perm/granted", UserId, "acme");
        app.Clock.Advance(TimeSpan.FromSeconds(59));
        await Send(app, "/perm/granted", UserId, "acme");

        Assert.Equal(1, app.Permissions.Calls);
    }

    [Fact]
    public async Task InvalidationAppliesOnNextRequest()
    {
        await using var app = await StartAsync();
        app.Permissions.Grant(UserId, "acme", "orders.view");
        await Send(app, "/perm/granted", UserId, "acme");

        app.Permissions.Grant(UserId, "acme", "orders.view", "orders.cancel");
        app.PermissionCache.Invalidate("acme", UserId);
        var body = await JsonAsync(await Send(app, "/perm/granted", UserId, "acme"));

        Assert.Equal(2, app.Permissions.Calls);
        Assert.Equal(["orders.cancel", "orders.view"], body.EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public async Task EntryExpiresAfterDuration()
    {
        await using var app = await StartAsync();

        await Send(app, "/perm/granted", UserId, "acme");
        app.Clock.Advance(TimeSpan.FromSeconds(60));
        await Send(app, "/perm/granted", UserId, "acme");

        Assert.Equal(2, app.Permissions.Calls);
    }

    [Fact]
    public async Task EntriesAreSeparatedByTenantAndUser()
    {
        await using var app = await StartAsync();
        app.Permissions.Grant(UserId, "acme", "a");
        app.Permissions.Grant(UserId, "globex", "g");
        app.Permissions.Grant(OtherUserId, "acme", "o");

        var acme = await JsonAsync(await Send(app, "/perm/granted", UserId, "acme"));
        var globex = await JsonAsync(await Send(app, "/perm/granted", UserId, "globex"));
        var other = await JsonAsync(await Send(app, "/perm/granted", OtherUserId, "acme"));

        Assert.Equal(3, app.Permissions.Calls);
        Assert.Equal(["a"], acme.EnumerateArray().Select(e => e.GetString()));
        Assert.Equal(["g"], globex.EnumerateArray().Select(e => e.GetString()));
        Assert.Equal(["o"], other.EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public async Task ProviderFailureIsNotCached()
    {
        await using var app = await StartAsync();
        app.Permissions.FailNextCall();

        var failed = await Send(app, "/perm/granted", UserId, "acme");
        var recovered = await Send(app, "/perm/granted", UserId, "acme");

        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);
        Assert.Equal(2, app.Permissions.Calls);
    }

    [Fact]
    public async Task InvalidatingUnknownEntryDoesNothing()
    {
        await using var app = await StartAsync();

        app.PermissionCache.Invalidate("nobody", Guid.NewGuid());
        await Send(app, "/perm/granted", UserId, "acme");

        Assert.Equal(1, app.Permissions.Calls);
    }

    [Fact]
    public async Task PlatformUserWithoutTenantIsCachedSeparately()
    {
        await using var app = await StartAsync();
        app.Permissions.Grant(UserId, null, "tenants.manage");

        var first = await JsonAsync(await Send(app, "/perm/granted", UserId, null, "platform"));
        await Send(app, "/perm/granted", UserId, null, "platform");
        await Send(app, "/perm/granted", UserId, "acme");

        Assert.Equal(["tenants.manage"], first.EnumerateArray().Select(e => e.GetString()));
        Assert.Equal(2, app.Permissions.Calls);
    }

    private static Task<HttpResponseMessage> Send(TestApp app, string path, Guid userId, string? tenant, string userType = "staff") =>
        app.Client.SendAsync(Get(path, TestAuthHandler.Claims(userId.ToString(), tenant, userType)), TestContext.Current.CancellationToken);
}
