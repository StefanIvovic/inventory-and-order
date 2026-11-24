using System.Net;
using Microsoft.Extensions.Logging;
using static BuildingBlocks.AspNetCore.Tests.TestApp;

namespace BuildingBlocks.AspNetCore.Tests.Errors;

public sealed class ProblemMapperTests
{
    [Fact]
    public async Task FirstRegisteredMapperClaimingTheExceptionShapesTheProblem()
    {
        await using var app = await StartAsync();

        var response = await app.Client.SendAsync(Get("/teapot"), TestContext.Current.CancellationToken);
        var body = await JsonAsync(response);

        Assert.Equal((HttpStatusCode)418, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("teapot", body.GetProperty("code").GetString());
        Assert.Equal("I am a teapot.", body.GetProperty("detail").GetString());
        Assert.True(body.GetProperty("brewed").GetBoolean());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("traceId").GetString()));
        Assert.Contains(app.Logs.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("teapot", StringComparison.Ordinal));
        Assert.DoesNotContain(app.Logs.Entries, e => e.Level >= LogLevel.Error);
    }

    [Fact]
    public async Task UnclaimedExceptionsKeepTheBuiltInMapping()
    {
        await using var app = await StartAsync();

        var rule = await app.Client.SendAsync(Get("/rule"), TestContext.Current.CancellationToken);
        var boom = await app.Client.SendAsync(Get("/boom"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, rule.StatusCode);
        Assert.Equal(HttpStatusCode.InternalServerError, boom.StatusCode);
    }
}
