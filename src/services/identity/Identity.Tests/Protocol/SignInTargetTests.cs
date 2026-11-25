using Identity.Api.Protocol;
using Identity.Api.Users;

namespace Identity.Tests.Protocol;

public sealed class SignInTargetTests
{
    [Fact]
    public void StorefrontAuthorizationRequestTargetsItsTenant()
    {
        var target = SignInTarget.From("/connect/authorize?client_id=storefront-bff&tenant=acme&scope=openid%20identity");

        Assert.Equal(ClientPolicies.Storefront, target.ClientId);
        Assert.Equal("acme", target.TenantSlug);
        Assert.True(target.IsCustomer);
    }

    [Theory]
    [InlineData("/connect/authorize?client_id=backoffice-bff&tenant=acme")]
    [InlineData("/connect/authorize?client_id=storefront-bff")]
    [InlineData("/account/signed-in?client_id=storefront-bff&tenant=acme")]
    [InlineData("https://evil.test/connect/authorize?client_id=storefront-bff&tenant=acme")]
    [InlineData("")]
    [InlineData(null)]
    public void EverythingElseTargetsStaffAndPlatform(string? returnUrl)
    {
        var target = SignInTarget.From(returnUrl);

        Assert.False(target.IsCustomer);
        Assert.Null(target.TenantSlug);
    }

    [Fact]
    public void PoliciesAcceptOnlyTheirUserTypes()
    {
        Assert.True(ClientPolicies.Accepts(ClientPolicies.Backoffice, UserTypes.Staff));
        Assert.True(ClientPolicies.Accepts(ClientPolicies.Backoffice, UserTypes.Platform));
        Assert.False(ClientPolicies.Accepts(ClientPolicies.Backoffice, UserTypes.Customer));
        Assert.True(ClientPolicies.Accepts(ClientPolicies.Storefront, UserTypes.Customer));
        Assert.False(ClientPolicies.Accepts(ClientPolicies.Storefront, UserTypes.Staff));
        Assert.False(ClientPolicies.Accepts(ClientPolicies.Storefront, UserTypes.Platform));
        Assert.False(ClientPolicies.Accepts("unknown-client", UserTypes.Staff));
        Assert.True(ClientPolicies.RequiresTenant(ClientPolicies.Storefront));
        Assert.False(ClientPolicies.RequiresTenant(ClientPolicies.Backoffice));
    }
}
