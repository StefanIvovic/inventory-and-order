using System.Net;
using Microsoft.Extensions.Logging;
using static BuildingBlocks.AspNetCore.Tests.TestApp;

namespace BuildingBlocks.AspNetCore.Tests.Errors;

public sealed class ErrorMappingTests
{
    [Theory]
    [InlineData("/conflict", HttpStatusCode.Conflict, "order.cancel.already-shipped", "The order has already shipped.")]
    [InlineData("/rule", HttpStatusCode.UnprocessableEntity, "inventory.transfer.insufficient-stock", "Not enough stock in the source warehouse.")]
    [InlineData("/missing", HttpStatusCode.NotFound, "not-found", "The requested order was not found.")]
    public async Task DomainErrorsMapToProblemDetails(string path, HttpStatusCode status, string code, string detail)
    {
        await using var app = await StartAsync();

        var response = await app.Client.SendAsync(Get(path), TestContext.Current.CancellationToken);
        var body = await JsonAsync(response);

        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal((int)status, body.GetProperty("status").GetInt32());
        Assert.Equal(code, body.GetProperty("code").GetString());
        Assert.Equal(detail, body.GetProperty("detail").GetString());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("title").GetString()));
        Assert.False(string.IsNullOrEmpty(body.GetProperty("type").GetString()));

        var traceId = body.GetProperty("traceId").GetString();
        Assert.False(string.IsNullOrEmpty(traceId));
        Assert.Contains(app.Logs.Entries, e => e.Level == LogLevel.Information && e.Message.Contains(code, StringComparison.Ordinal) && e.Message.Contains(traceId!, StringComparison.Ordinal));
        Assert.DoesNotContain(app.Logs.Entries, e => e.Level >= LogLevel.Error);
    }

    [Theory]
    [InlineData("/boom")]
    [InlineData("/early")]
    public async Task UnexpectedErrorsMapTo500WithoutLeakingDetails(string path)
    {
        await using var app = await StartAsync();

        var response = await app.Client.SendAsync(Get(path), TestContext.Current.CancellationToken);
        var raw = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var body = await JsonAsync(response);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.DoesNotContain(Secret, raw, StringComparison.Ordinal);
        Assert.DoesNotContain(" at ", raw, StringComparison.Ordinal);
        Assert.False(body.TryGetProperty("code", out _));
        Assert.False(body.TryGetProperty("detail", out _));

        var traceId = body.GetProperty("traceId").GetString();
        Assert.False(string.IsNullOrEmpty(traceId));
        Assert.Contains(app.Logs.Entries, e => e.Level == LogLevel.Error && e.Exception?.Message == Secret && e.Message.Contains(traceId!, StringComparison.Ordinal));
    }
}
