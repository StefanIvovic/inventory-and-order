using System.Net;
using static BuildingBlocks.AspNetCore.Tests.TestApp;

namespace BuildingBlocks.AspNetCore.Tests;

public sealed class AdoptionTests
{
    private const string UserId = "6f9619ff-8b86-d011-b42d-00cf4fc964ff";

    [Fact]
    public async Task TwoSetupCallsEnableIdentityTenancyAndErrorMapping()
    {
        await using var app = await StartAsync();
        var cancellation = TestContext.Current.CancellationToken;
        var staff = TestAuthHandler.Claims(UserId, "acme", "staff");

        var me = await app.Client.SendAsync(Get("/me", staff), cancellation);
        var tenant = await app.Client.SendAsync(Get("/tenant", staff), cancellation);
        var rule = await app.Client.SendAsync(Get("/rule", staff), cancellation);
        var invalid = await app.Client.SendAsync(Get("/me", TestAuthHandler.Claims(UserId, null, "staff")), cancellation);

        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.Equal("acme", await tenant.Content.ReadAsStringAsync(cancellation));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, rule.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, invalid.StatusCode);
    }
}
