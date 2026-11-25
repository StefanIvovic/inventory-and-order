using Gateway.Api.Sessions;

namespace Gateway.Tests;

public sealed class ReturnPathTests
{
    [Theory]
    [InlineData(null, "/backoffice/")]
    [InlineData("", "/backoffice/")]
    [InlineData("/backoffice/orders/7?tab=lines#top", "/backoffice/orders/7?tab=lines#top")]
    [InlineData("/backoffice", "/backoffice/")]
    [InlineData("https://evil.test/backoffice/", "/backoffice/")]
    [InlineData("//evil.test/", "/backoffice/")]
    [InlineData("/backoffice//evil.test", "/backoffice/")]
    [InlineData("/backoffice/\\evil", "/backoffice/")]
    [InlineData("/backoffice/../bff/backoffice/user", "/backoffice/")]
    [InlineData("/backoffice/./x", "/backoffice/")]
    [InlineData("/backoffice/\nx", "/backoffice/")]
    [InlineData("/shop/acme/", "/backoffice/")]
    public void BackofficeReturnPaths(string? returnUrl, string expected) => Assert.Equal(expected, ReturnPaths.Resolve(returnUrl, "/backoffice/"));

    [Theory]
    [InlineData("/shop/acme/cart", "/shop/acme/cart")]
    [InlineData("/shop/globex/cart", "/shop/acme/")]
    [InlineData("/shop/acmex/", "/shop/acme/")]
    public void StorefrontReturnPathsStayInTheShop(string returnUrl, string expected) => Assert.Equal(expected, ReturnPaths.Resolve(returnUrl, "/shop/acme/"));
}

public sealed class SlugTests
{
    [Theory]
    [InlineData("acme")]
    [InlineData("demo")]
    [InlineData("a1b")]
    [InlineData("my-shop")]
    [InlineData("a-b-c")]
    [InlineData("abcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabc")]
    public void AcceptsIdentitySlugs(string slug) => Assert.True(ReturnPaths.IsValidSlug(slug));

    [Theory]
    [InlineData("ab")]
    [InlineData("Acme")]
    [InlineData("-acme")]
    [InlineData("acme-")]
    [InlineData("ac--me")]
    [InlineData("ac_me")]
    [InlineData("ac.me")]
    [InlineData("abcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcd")]
    public void RejectsMalformedSlugs(string slug) => Assert.False(ReturnPaths.IsValidSlug(slug));
}

public sealed class AppsTests
{
    [Fact]
    public void CookieNamesArePrefixedOnlyOverHttps()
    {
        Assert.Equal("__Host-Http-bff-backoffice", BffApp.Backoffice.CookieName(https: true));
        Assert.Equal("bff-backoffice", BffApp.Backoffice.CookieName(https: false));
        Assert.Equal("__Host-Http-bff-storefront", BffApp.Storefront.CookieName(https: true));
        Assert.Equal("bff-storefront", BffApp.Storefront.CookieName(https: false));
    }

    [Fact]
    public void AppsUseTheirRegisteredCallbacksAndStartPages()
    {
        Assert.Equal("/bff/backoffice/signin-oidc", BffApp.Backoffice.CallbackPath);
        Assert.Equal("/bff/storefront/signin-oidc", BffApp.Storefront.CallbackPath);
        Assert.Equal("/bff/backoffice/signout-callback-oidc", BffApp.Backoffice.SignedOutCallbackPath);
        Assert.Equal("/bff/storefront/signout-callback-oidc", BffApp.Storefront.SignedOutCallbackPath);
        Assert.Equal("/backoffice/", BffApp.Backoffice.StartPage(null));
        Assert.Equal("/shop/acme/", BffApp.Storefront.StartPage("acme"));
        Assert.Same(BffApp.Storefront, BffApp.FromHeader("storefront"));
        Assert.Null(BffApp.FromHeader("Backoffice"));
    }
}
