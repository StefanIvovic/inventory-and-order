using System.Net;
using Microsoft.Extensions.Logging;
using static BuildingBlocks.AspNetCore.Tests.TestApp;

namespace BuildingBlocks.AspNetCore.Tests.Permissions;

public sealed class PermissionDeniedMappingTests
{
    private static readonly Guid UserId = Guid.Parse("6f9619ff-8b86-d011-b42d-00cf4fc964ff");
    private static readonly string Staff = TestAuthHandler.Claims(UserId.ToString(), "acme", "staff");

    [Fact]
    public async Task DeniedCheckBecomesStandard403()
    {
        await using var app = await StartAsync();

        var response = await app.Client.SendAsync(Get("/perm/ensure?p=orders.cancel", Staff), TestContext.Current.CancellationToken);
        var body = await JsonAsync(response);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(403, body.GetProperty("status").GetInt32());
        Assert.Equal("permission-denied", body.GetProperty("code").GetString());
        Assert.Equal(["orders.cancel"], body.GetProperty("missingPermissions").EnumerateArray().Select(e => e.GetString()));
        Assert.False(string.IsNullOrEmpty(body.GetProperty("detail").GetString()));
        Assert.False(string.IsNullOrEmpty(body.GetProperty("traceId").GetString()));
    }

    [Fact]
    public async Task DeniedCheckIsLoggedAsWarningWithUserMissingAndTrace()
    {
        await using var app = await StartAsync();

        var body = await JsonAsync(await app.Client.SendAsync(Get("/perm/ensure-all?p=orders.cancel&p=orders.refund", Staff), TestContext.Current.CancellationToken));
        var traceId = body.GetProperty("traceId").GetString()!;

        Assert.Contains(app.Logs.Entries, e =>
            e.Level == LogLevel.Warning &&
            e.Message.Contains(UserId.ToString(), StringComparison.Ordinal) &&
            e.Message.Contains("orders.cancel, orders.refund", StringComparison.Ordinal) &&
            e.Message.Contains(traceId, StringComparison.Ordinal));
        Assert.DoesNotContain(app.Logs.Entries, e => e.Level >= LogLevel.Error);
    }
}
