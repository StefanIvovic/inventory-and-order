using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace Gateway.Tests;

public sealed class StorefrontSignInTests
{
    [Fact]
    public async Task LoginSendsTheTenantSlugAsTheStorefrontClient()
    {
        await using var app = await GatewayApp.StartAsync();
        using var browser = app.CreateBrowser();

        var response = await browser.GetAsync("/bff/shop/acme/login");

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        var query = QueryHelpers.ParseQuery(response.Response.Headers.Location!.Query);
        Assert.Equal("storefront-bff", query["client_id"]);
        Assert.Equal("acme", query["tenant"]);
    }

    [Fact]
    public async Task SignInLandsInTheShopAndTheUserEndpointIsPerSlug()
    {
        await using var app = await GatewayApp.StartAsync();
        app.Identity.User = new Fakes.FakeUser("customer-1", "customer", "tenant-acme", "shopper@mail.test");
        using var browser = await app.SignedInStorefrontAsync("acme");

        Assert.Equal(["__Host-Http-bff-storefront"], browser.CookieNames());
        var acme = await browser.GetAsync("/bff/shop/acme/user");
        Assert.Equal(HttpStatusCode.OK, acme.Status);
        using var json = JsonDocument.Parse(acme.Body);
        Assert.Equal("acme", json.RootElement.GetProperty("slug").GetString());
        Assert.Equal("customer", json.RootElement.GetProperty("userType").GetString());
        Assert.StartsWith("/bff/shop/acme/logout?sid=", json.RootElement.GetProperty("logoutUrl").GetString(), StringComparison.Ordinal);

        var globex = await browser.GetAsync("/bff/shop/globex/user");
        Assert.Equal(HttpStatusCode.Unauthorized, globex.Status);
        Assertions.NoTokens(app, browser);
    }

    [Fact]
    public async Task ReturnPathStaysInsideTheShop()
    {
        await using var app = await GatewayApp.StartAsync();
        using var browser = app.CreateBrowser();

        var inside = await browser.NavigateAsync("/bff/shop/acme/login?returnUrl=" + Uri.EscapeDataString("/shop/acme/cart"));
        var outside = await browser.NavigateAsync("/bff/shop/acme/login?returnUrl=" + Uri.EscapeDataString("/shop/globex/cart"));

        Assert.Equal("/shop/acme/cart", inside.Uri.AbsolutePath);
        Assert.Equal("/shop/acme/", outside.Uri.AbsolutePath);
    }

    [Fact]
    public async Task SigningInToAnotherShopReplacesTheSession()
    {
        await using var app = await GatewayApp.StartAsync();
        using var browser = await app.SignedInStorefrontAsync("acme");

        await app.SignedInStorefrontAsync("globex", browser);

        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.GetAsync("/bff/shop/acme/user")).Status);
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/bff/shop/globex/user")).Status);
    }

    [Theory]
    [InlineData("Acme")]
    [InlineData("ac--me")]
    [InlineData("a")]
    public async Task MalformedSlugIs404WithoutContactingIdentity(string slug)
    {
        await using var app = await GatewayApp.StartAsync();
        using var browser = app.CreateBrowser();

        var response = await browser.GetAsync($"/bff/shop/{slug}/login");

        Assert.Equal(HttpStatusCode.NotFound, response.Status);
        Assert.Empty(app.Identity.Requests);
    }

    [Fact]
    public async Task BackofficeAndStorefrontSessionsAreSeparate()
    {
        await using var app = await GatewayApp.StartAsync();
        using var browser = await app.SignedInBackofficeAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.GetAsync("/bff/shop/acme/user")).Status);

        await app.SignedInStorefrontAsync("acme", browser);

        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/bff/backoffice/user")).Status);
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/bff/shop/acme/user")).Status);
        Assert.Equal(2, browser.CookieNames().Count());
    }
}
