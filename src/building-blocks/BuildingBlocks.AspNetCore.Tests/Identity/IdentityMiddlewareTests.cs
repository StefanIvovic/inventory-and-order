using System.Net;
using Microsoft.Extensions.Logging;
using static BuildingBlocks.AspNetCore.Tests.TestApp;

namespace BuildingBlocks.AspNetCore.Tests.Identity;

public sealed class IdentityMiddlewareTests
{
    private const string UserId = "6f9619ff-8b86-d011-b42d-00cf4fc964ff";

    [Fact]
    public async Task ValidStaffIdentityIsAvailableToHandler()
    {
        await using var app = await StartAsync();

        var response = await app.Client.SendAsync(Get("/me", TestAuthHandler.Claims(UserId, "acme", "staff")), TestContext.Current.CancellationToken);
        var body = await JsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(body.GetProperty("authenticated").GetBoolean());
        Assert.Equal(UserId, body.GetProperty("userId").GetString());
        Assert.Equal("acme", body.GetProperty("tenantId").GetString());
        Assert.Equal("Staff", body.GetProperty("userType").GetString());
    }

    [Fact]
    public async Task UnauthenticatedRequestReportsNotAuthenticated()
    {
        await using var app = await StartAsync();

        var response = await app.Client.SendAsync(Get("/me"), TestContext.Current.CancellationToken);
        var body = await JsonAsync(response);

        Assert.False(body.GetProperty("authenticated").GetBoolean());
    }

    [Fact]
    public async Task ReadingUserIdWhenUnauthenticatedThrows()
    {
        await using var app = await StartAsync();

        var response = await app.Client.SendAsync(Get("/me/id"), TestContext.Current.CancellationToken);

        Assert.Equal("threw", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("not-a-guid", "acme", "staff")]
    [InlineData(UserId, "acme", "admin")]
    [InlineData(UserId, null, "staff")]
    [InlineData(UserId, null, "customer")]
    [InlineData(UserId, "", "staff")]
    public async Task MalformedIdentityIsRejectedBeforeHandler(string subject, string? tenant, string userType)
    {
        await using var app = await StartAsync();

        var response = await app.Client.SendAsync(Get("/me", TestAuthHandler.Claims(subject, tenant, userType)), TestContext.Current.CancellationToken);
        var body = await JsonAsync(response);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("invalid-identity", body.GetProperty("code").GetString());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("traceId").GetString()));
        Assert.Equal(0, app.Invocations.Count);
        Assert.Contains(app.Logs.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("Rejected request identity", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RejectionLogDoesNotContainClaimValues()
    {
        await using var app = await StartAsync();

        await app.Client.SendAsync(Get("/me", TestAuthHandler.Claims("distinctive-subject-value", "acme", "staff")), TestContext.Current.CancellationToken);

        Assert.DoesNotContain(app.Logs.Entries, e => e.Message.Contains("distinctive-subject-value", StringComparison.Ordinal));
    }
}
