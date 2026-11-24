using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Validation.Tests;

public sealed class HttpValidationTests
{
    [Fact]
    public async Task AllFieldErrorsAreReturnedAtOnceGroupedByClientField()
    {
        await using var app = await TestApp.StartAsync();
        var command = new CreateThing("", -1, Guid.Empty, [new ThingLine("ok"), new ThingLine("")]);

        var response = await app.Client.PostAsJsonAsync("/things", command, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var errors = body.GetProperty("errors");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(400, body.GetProperty("status").GetInt32());
        Assert.Equal("validation-failed", body.GetProperty("code").GetString());
        Assert.Equal("One or more validation errors occurred.", body.GetProperty("title").GetString());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("type").GetString()));
        Assert.False(string.IsNullOrEmpty(body.GetProperty("traceId").GetString()));
        Assert.Equal(["lines[1].sku", "name", "quantity", "warehouseId"], errors.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
        Assert.Equal(1, errors.GetProperty("name").GetArrayLength());
        Assert.Equal(1, errors.GetProperty("quantity").GetArrayLength());
        Assert.Equal(2, errors.GetProperty("warehouseId").GetArrayLength());
        Assert.Equal(1, errors.GetProperty("lines[1].sku").GetArrayLength());
        Assert.Equal(0, app.Counter.Of(nameof(CreateThing)));
    }

    [Fact]
    public async Task ErrorsFromAllValidatorsAreCombined()
    {
        await using var app = await TestApp.StartAsync();
        var command = new CreateThing("ok", -1, Guid.NewGuid(), []);

        var response = await app.Client.PostAsJsonAsync("/things", command, TestContext.Current.CancellationToken);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("errors");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(errors.TryGetProperty("quantity", out _));
        Assert.True(errors.TryGetProperty("lines", out _));
    }

    [Fact]
    public async Task ValidCommandReachesTheHandler()
    {
        await using var app = await TestApp.StartAsync();
        var command = new CreateThing("ok", 3, Guid.NewGuid(), [new ThingLine("sku-1")]);

        var response = await app.Client.PostAsJsonAsync("/things", command, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, app.Counter.Of(nameof(CreateThing)));
    }

    [Fact]
    public async Task ManualValidationExceptionHasTheSameShapeAndLogsNoValues()
    {
        await using var app = await TestApp.StartAsync();

        var response = await app.Client.PostAsJsonAsync("/manual", new ManualThing(ManualThingHandler.SecretValue), TestContext.Current.CancellationToken);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var traceId = body.GetProperty("traceId").GetString()!;

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("validation-failed", body.GetProperty("code").GetString());
        Assert.Equal(["address.postalCode", "code"], body.GetProperty("errors").EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
        Assert.Contains(app.Logs.Entries, e => e.Level == LogLevel.Information && e.Message.Contains("validation-failed", StringComparison.Ordinal) && e.Message.Contains(traceId, StringComparison.Ordinal));
        Assert.DoesNotContain(app.Logs.Entries, e => e.Level >= LogLevel.Error);
        Assert.DoesNotContain(app.Logs.Entries, e => e.Message.Contains(ManualThingHandler.SecretValue, StringComparison.Ordinal));
    }
}
