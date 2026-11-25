using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Lookups.Tests;

public sealed class LookupCachingTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task OkAnswerCarriesTagAndCacheHeaders()
    {
        await using var app = await StartAsync();

        using var response = await app.GetTestClient().GetAsync(new Uri("/values?v=1", UriKind.Relative), Cancellation);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(response.Headers.ETag);
        Assert.False(response.Headers.ETag.IsWeak);
        Assert.True(response.Headers.CacheControl?.Private);
        Assert.Equal(TimeSpan.FromSeconds(LookupCaching.MaxAgeSeconds), response.Headers.CacheControl?.MaxAge);
        Assert.Contains("Cookie", response.Headers.Vary);
        Assert.Contains("Authorization", response.Headers.Vary);
        Assert.Equal("{\"value\":\"1\"}", await response.Content.ReadAsStringAsync(Cancellation));
    }

    [Fact]
    public async Task TagIsStableForTheSameBodyAndChangesWithIt()
    {
        await using var app = await StartAsync();
        var client = app.GetTestClient();

        var first = await TagOf(client, "/values?v=1");
        var again = await TagOf(client, "/values?v=1");
        var other = await TagOf(client, "/values?v=2");

        Assert.Equal(first, again);
        Assert.NotEqual(first, other);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MatchingTagIsNotModifiedWithNoBody(bool weak)
    {
        await using var app = await StartAsync();
        var client = app.GetTestClient();
        var tag = await TagOf(client, "/values?v=1");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/values?v=1");
        request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue(tag.Tag, weak));
        using var response = await client.SendAsync(request, Cancellation);

        Assert.Equal(HttpStatusCode.NotModified, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync(Cancellation));
        Assert.Equal(tag.Tag, response.Headers.ETag?.Tag);
    }

    [Fact]
    public async Task AnyTagMatches()
    {
        await using var app = await StartAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/values?v=1");
        request.Headers.IfNoneMatch.Add(EntityTagHeaderValue.Any);

        using var response = await app.GetTestClient().SendAsync(request, Cancellation);

        Assert.Equal(HttpStatusCode.NotModified, response.StatusCode);
    }

    [Fact]
    public async Task StaleTagGetsTheBody()
    {
        await using var app = await StartAsync();
        var client = app.GetTestClient();
        var stale = await TagOf(client, "/values?v=2");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/values?v=1");
        request.Headers.IfNoneMatch.Add(stale);
        using var response = await client.SendAsync(request, Cancellation);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("{\"value\":\"1\"}", await response.Content.ReadAsStringAsync(Cancellation));
    }

    [Fact]
    public async Task UnmarkedEndpointsAndErrorsAreUntouched()
    {
        await using var app = await StartAsync();
        var client = app.GetTestClient();

        using var plain = await client.GetAsync(new Uri("/plain", UriKind.Relative), Cancellation);
        using var missing = await client.GetAsync(new Uri("/missing", UriKind.Relative), Cancellation);

        Assert.Equal(HttpStatusCode.OK, plain.StatusCode);
        Assert.Null(plain.Headers.ETag);
        Assert.Null(plain.Headers.CacheControl);
        Assert.Equal("plain", await plain.Content.ReadAsStringAsync(Cancellation));
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Null(missing.Headers.ETag);
        Assert.Contains("gone", await missing.Content.ReadAsStringAsync(Cancellation), StringComparison.Ordinal);
    }

    private static async Task<EntityTagHeaderValue> TagOf(HttpClient client, string path)
    {
        using var response = await client.GetAsync(new Uri(path, UriKind.Relative), Cancellation);
        return response.Headers.ETag ?? throw new InvalidOperationException("No ETag.");
    }

    private static async Task<WebApplication> StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        var app = builder.Build();
        app.UseLookupCaching();
        app.MapGet("/values", (string v) => Results.Json(new { value = v })).WithLookupCaching();
        app.MapGet("/missing", () => Results.NotFound(new { error = "gone" })).WithLookupCaching();
        app.MapGet("/plain", () => "plain");
        await app.StartAsync(Cancellation);
        return app;
    }
}
