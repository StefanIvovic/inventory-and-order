using Gateway.Tests.Fakes;

namespace Gateway.Tests;

public static class Assertions
{
    public static void NoTokens(GatewayApp app, Browser browser)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(browser);
        var pages = browser.History.Where(entry => !entry.Uri.AbsolutePath.StartsWith("/api/", StringComparison.Ordinal)).ToList();
        foreach (var token in app.Identity.IssuedAccessTokens)
        {
            Assert.All(pages, entry =>
            {
                Assert.DoesNotContain(token, entry.Body, StringComparison.Ordinal);
                Assert.DoesNotContain(entry.Response.Headers, header => header.Value.Any(value => value.Contains(token, StringComparison.Ordinal)));
            });
        }

        Assert.All(pages, entry => Assert.DoesNotContain("rt-", entry.Body, StringComparison.Ordinal));
    }
}
