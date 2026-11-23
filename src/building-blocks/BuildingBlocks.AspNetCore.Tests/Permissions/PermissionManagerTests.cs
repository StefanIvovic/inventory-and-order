using System.Net;
using System.Text.Json;
using static BuildingBlocks.AspNetCore.Tests.TestApp;

namespace BuildingBlocks.AspNetCore.Tests.Permissions;

public sealed class PermissionManagerTests
{
    private static readonly Guid UserId = Guid.Parse("6f9619ff-8b86-d011-b42d-00cf4fc964ff");
    private static readonly string Staff = TestAuthHandler.Claims(UserId.ToString(), "acme", "staff");

    [Theory]
    [InlineData("/perm/has?p=inventory.adjust", "true")]
    [InlineData("/perm/has?p=inventory.transfer", "false")]
    [InlineData("/perm/has?p=Inventory.Adjust", "false")]
    [InlineData("/perm/all?p=inventory.adjust&p=inventory.transfer", "false")]
    [InlineData("/perm/all?p=inventory.adjust&p=inventory.view", "true")]
    [InlineData("/perm/any?p=inventory.adjust&p=inventory.transfer", "true")]
    [InlineData("/perm/any?p=inventory.audit&p=inventory.transfer", "false")]
    public async Task ChecksAnswerFromProvider(string path, string expected)
    {
        await using var app = await StartAsync();
        app.Permissions.Grant(UserId, "acme", "inventory.view", "inventory.adjust");

        var response = await app.Client.SendAsync(Get(path, Staff), TestContext.Current.CancellationToken);

        Assert.Equal(expected, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task EnsureReportsSingleMissingPermission()
    {
        await using var app = await StartAsync();
        app.Permissions.Grant(UserId, "acme", "inventory.view", "inventory.adjust");

        var body = await JsonAsync(await app.Client.SendAsync(Get("/perm/ensure?p=inventory.transfer", Staff), TestContext.Current.CancellationToken));

        Assert.Equal(["inventory.transfer"], Missing(body));
    }

    [Fact]
    public async Task EnsureAllReportsExactlyTheMissingInOrder()
    {
        await using var app = await StartAsync();
        app.Permissions.Grant(UserId, "acme", "inventory.view", "inventory.adjust");

        var body = await JsonAsync(await app.Client.SendAsync(
            Get("/perm/ensure-all?p=inventory.view&p=inventory.transfer&p=inventory.audit", Staff),
            TestContext.Current.CancellationToken));

        Assert.Equal(["inventory.transfer", "inventory.audit"], Missing(body));
    }

    [Fact]
    public async Task EnsureSucceedsWhenGranted()
    {
        await using var app = await StartAsync();
        app.Permissions.Grant(UserId, "acme", "inventory.adjust");

        var response = await app.Client.SendAsync(Get("/perm/ensure?p=inventory.adjust", Staff), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task EnsureAllWithEmptyListSucceeds()
    {
        await using var app = await StartAsync();

        var response = await app.Client.SendAsync(Get("/perm/ensure-all", Staff), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GrantedReturnsFullSet()
    {
        await using var app = await StartAsync();
        app.Permissions.Grant(UserId, "acme", "inventory.view", "inventory.adjust");

        var body = await JsonAsync(await app.Client.SendAsync(Get("/perm/granted", Staff), TestContext.Current.CancellationToken));

        Assert.Equal(["inventory.adjust", "inventory.view"], body.EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public async Task UnauthenticatedCallerHasNothingAndProviderIsNotCalled()
    {
        await using var app = await StartAsync();

        var granted = await JsonAsync(await app.Client.SendAsync(Get("/perm/granted"), TestContext.Current.CancellationToken));
        var denied = await app.Client.SendAsync(Get("/perm/ensure?p=inventory.view"), TestContext.Current.CancellationToken);

        Assert.Equal(0, granted.GetArrayLength());
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal(0, app.Permissions.Calls);
    }

    private static string?[] Missing(JsonElement body) =>
        body.GetProperty("missingPermissions").EnumerateArray().Select(e => e.GetString()).ToArray();
}
