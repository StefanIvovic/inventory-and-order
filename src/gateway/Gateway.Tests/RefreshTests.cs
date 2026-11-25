using System.Net;
using System.Text.Json;
using Gateway.Api.Sessions;
using Gateway.Tests.Fakes;

namespace Gateway.Tests;

public sealed class RefreshTests
{
    private static readonly TimeSpan PastThreshold = TimeSpan.FromMinutes(14.5);

    [Fact]
    public async Task BeforeTheThresholdThereIsNoRefresh()
    {
        await using var app = await GatewayApp.StartAsync();
        using var browser = await app.SignedInBackofficeAsync();
        app.Clock.Advance(TimeSpan.FromMinutes(13));

        var response = await CallAsync(browser);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Equal(0, app.Identity.RefreshCount);
        Assert.DoesNotContain(response.Response.Headers, header => header.Key == "Set-Cookie");
    }

    [Fact]
    public async Task PastTheThresholdTheCallRefreshesForwardsTheNewTokenAndUpdatesTheCookie()
    {
        await using var app = await GatewayApp.StartAsync();
        using var browser = await app.SignedInBackofficeAsync();
        app.Clock.Advance(PastThreshold);

        var response = await CallAsync(browser);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Equal(1, app.Identity.RefreshCount);
        Assert.Equal("Bearer " + app.Identity.IssuedAccessTokens.Last(), Echo(response).Headers["Authorization"]);
        Assert.Contains(response.Response.Headers.GetValues("Set-Cookie"), header => header.StartsWith("__Host-Http-bff-backoffice=", StringComparison.Ordinal));

        var next = await CallAsync(browser);
        Assert.Equal(1, app.Identity.RefreshCount);
        Assert.Equal("Bearer " + app.Identity.IssuedAccessTokens.Last(), Echo(next).Headers["Authorization"]);
        Assertions.NoTokens(app, browser);
    }

    [Fact]
    public async Task ParallelCallsPastTheThresholdShareOneRefresh()
    {
        await using var app = await GatewayApp.StartAsync();
        using var browser = await app.SignedInBackofficeAsync();
        app.Identity.RefreshDelay = TimeSpan.FromMilliseconds(300);
        app.Clock.Advance(PastThreshold);
        var cookie = browser.Cookies.GetCookieHeader(browser.Origin);

        var calls = Enumerable.Range(0, 10).Select(_ => RawCallAsync(app, cookie)).ToList();
        var responses = await Task.WhenAll(calls);

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        Assert.Equal(1, app.Identity.RefreshCount);
        foreach (var response in responses)
        {
            response.Dispose();
        }
    }

    [Fact]
    public async Task StaleCookieAfterARefreshReusesTheResult()
    {
        await using var app = await GatewayApp.StartAsync();
        using var browser = await app.SignedInBackofficeAsync();
        var stale = browser.Cookies.GetCookieHeader(browser.Origin);
        app.Clock.Advance(PastThreshold);
        await CallAsync(browser);

        using var late = await RawCallAsync(app, stale);

        Assert.Equal(HttpStatusCode.OK, late.StatusCode);
        Assert.Equal(1, app.Identity.RefreshCount);
        var echo = JsonSerializer.Deserialize<EchoBody>(await late.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), JsonSerializerOptions.Web)!;
        Assert.Equal("Bearer " + app.Identity.IssuedAccessTokens.Last(), echo.Headers["Authorization"]);
    }

    [Fact]
    public async Task UserEndpointWithAStaleCookieNeverWritesTheCookieBack()
    {
        await using var app = await GatewayApp.StartAsync();
        using var browser = await app.SignedInBackofficeAsync();
        var stale = browser.Cookies.GetCookieHeader(browser.Origin);
        app.Clock.Advance(PastThreshold);
        await CallAsync(browser);
        app.Clock.Advance(TimeSpan.FromHours(7));

        using var client = app.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false, BaseAddress = app.Origin });
        using var request = new HttpRequestMessage(HttpMethod.Get, "/bff/backoffice/user");
        request.Headers.Add("Cookie", stale);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task RefreshMovesTheSessionExpiryTwelveHoursFromTheRefresh()
    {
        await using var app = await GatewayApp.StartAsync();
        using var browser = await app.SignedInBackofficeAsync();
        var original = browser.Cookies.GetCookieHeader(browser.Origin);
        app.Clock.Advance(TimeSpan.FromHours(11.9));
        await CallAsync(browser);
        app.Clock.Advance(TimeSpan.FromHours(1));

        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/bff/backoffice/user")).Status);
        using var client = app.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false, BaseAddress = app.Origin });
        using var request = new HttpRequestMessage(HttpMethod.Get, "/bff/backoffice/user");
        request.Headers.Add("Cookie", original);
        using var expired = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);

        app.Clock.Advance(TimeSpan.FromHours(11.5));
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.GetAsync("/bff/backoffice/user")).Status);
    }

    [Fact]
    public async Task AlreadyExpiredAccessTokenIsRefreshed()
    {
        await using var app = await GatewayApp.StartAsync();
        using var browser = await app.SignedInBackofficeAsync();
        app.Clock.Advance(TimeSpan.FromHours(3));

        var response = await CallAsync(browser);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Equal(1, app.Identity.RefreshCount);
    }

    [Fact]
    public async Task RefusedRefreshEndsTheSession()
    {
        await using var app = await GatewayApp.StartAsync();
        using var browser = await app.SignedInBackofficeAsync();
        app.Identity.Refresh = RefreshMode.Refuse;
        app.Clock.Advance(PastThreshold);

        var response = await CallAsync(browser);

        Assert.Equal(HttpStatusCode.Unauthorized, response.Status);
        Assert.Contains("session-ended", response.Body, StringComparison.Ordinal);
        Assert.Empty(app.Identity.RequestsTo("/x"));
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.GetAsync("/bff/backoffice/user")).Status);
    }

    [Fact]
    public async Task FailingIdentityIs503AndTheSessionSurvives()
    {
        await using var app = await GatewayApp.StartAsync();
        using var browser = await app.SignedInBackofficeAsync();
        app.Identity.Refresh = RefreshMode.Fail;
        app.Clock.Advance(PastThreshold);

        var failed = await CallAsync(browser);
        app.Identity.Refresh = RefreshMode.Normal;
        var recovered = await CallAsync(browser);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, failed.Status);
        Assert.Contains("identity-unavailable", failed.Body, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, recovered.Status);
    }

    private static Task<BrowserResponse> CallAsync(Browser browser) =>
        browser.GetAsync("/api/identity/x", request => request.Headers.Add("X-Bff-App", BffApp.Backoffice.Name));

    private static async Task<HttpResponseMessage> RawCallAsync(GatewayApp app, string cookie)
    {
        using var client = app.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false, BaseAddress = app.Origin });
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/identity/x");
        request.Headers.Add("Cookie", cookie);
        request.Headers.Add("X-Bff-App", "backoffice");
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        await response.Content.LoadIntoBufferAsync(TestContext.Current.CancellationToken);
        return response;
    }

    private static EchoBody Echo(BrowserResponse response) => JsonSerializer.Deserialize<EchoBody>(response.Body, JsonSerializerOptions.Web)!;
}
