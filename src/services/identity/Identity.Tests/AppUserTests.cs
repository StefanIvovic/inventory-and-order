using Identity.Api.Users;

namespace Identity.Tests;

public sealed class AppUserTests
{
    [Theory]
    [InlineData("acme", UserTypes.Platform)]
    [InlineData("acme", "admin")]
    [InlineData(" ", UserTypes.Staff)]
    public void TenantUserNeedsATenantAndATenantType(string tenantId, string userType)
    {
        Assert.ThrowsAny<ArgumentException>(() => AppUser.ForTenant("user@acme.test", tenantId, userType));
    }

    [Fact]
    public void PlatformUserHasNoTenant()
    {
        var user = AppUser.Platform("admin@platform.test");

        Assert.Equal(UserTypes.Platform, user.UserType);
        Assert.Null(user.TenantId);
    }
}
