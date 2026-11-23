using static BuildingBlocks.AspNetCore.Tests.TestApp;

namespace BuildingBlocks.AspNetCore.Tests.Handlers;

public sealed class CommandHandlerTests
{
    private static readonly Guid UserId = Guid.Parse("6f9619ff-8b86-d011-b42d-00cf4fc964ff");

    [Fact]
    public async Task HandlerSeesUserTenantPermissionsClockAndDb()
    {
        await using var app = await StartAsync();
        app.Permissions.Grant(UserId, "acme", "inventory.adjust");
        var claims = TestAuthHandler.Claims(UserId.ToString(), "acme", "staff");

        var first = await JsonAsync(await app.Client.SendAsync(Get("/handler", claims), TestContext.Current.CancellationToken));
        var second = await JsonAsync(await app.Client.SendAsync(Get("/handler", claims), TestContext.Current.CancellationToken));

        Assert.Equal(UserId, first.GetProperty("userId").GetGuid());
        Assert.Equal("acme", first.GetProperty("tenantId").GetString());
        Assert.Equal(app.Clock.GetUtcNow(), first.GetProperty("now").GetDateTimeOffset());
        Assert.True(first.GetProperty("canAdjust").GetBoolean());
        Assert.NotEqual(first.GetProperty("dbId").GetGuid(), second.GetProperty("dbId").GetGuid());
    }
}
