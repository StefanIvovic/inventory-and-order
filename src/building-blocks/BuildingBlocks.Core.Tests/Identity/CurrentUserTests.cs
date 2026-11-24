using System.Security.Claims;
using BuildingBlocks.Core.Identity;

namespace BuildingBlocks.Core.Tests.Identity;

public sealed class CurrentUserTests
{
    private static readonly Guid UserId = Guid.Parse("6f9619ff-8b86-d011-b42d-00cf4fc964ff");

    [Theory]
    [InlineData("staff", UserType.Staff)]
    [InlineData("customer", UserType.Customer)]
    [InlineData("Staff", UserType.Staff)]
    [InlineData("CUSTOMER", UserType.Customer)]
    public void TenantUserIsCreated(string userType, UserType expected)
    {
        var principal = Principal(UserId.ToString(), "acme", userType);

        Assert.True(CurrentUser.TryCreate(principal, out var user, out _));
        Assert.Equal(new CurrentUser(UserId, "acme", expected), user);
    }

    [Fact]
    public void PlatformUserWithoutTenantIsCreated()
    {
        var principal = Principal(UserId.ToString(), null, "platform");

        Assert.True(CurrentUser.TryCreate(principal, out var user, out _));
        Assert.Null(user.TenantId);
        Assert.Equal(UserType.Platform, user.UserType);
    }

    [Fact]
    public void PlatformUserWithTenantIsRejected()
    {
        var principal = Principal(UserId.ToString(), "acme", "platform");

        Assert.False(CurrentUser.TryCreate(principal, out var user, out var reason));
        Assert.Null(user);
        Assert.Contains("tenant_id", reason, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null, "acme", "staff")]
    [InlineData("not-a-guid", "acme", "staff")]
    [InlineData("6f9619ff-8b86-d011-b42d-00cf4fc964ff", "acme", null)]
    [InlineData("6f9619ff-8b86-d011-b42d-00cf4fc964ff", "acme", "admin")]
    [InlineData("6f9619ff-8b86-d011-b42d-00cf4fc964ff", "acme", "1")]
    [InlineData("6f9619ff-8b86-d011-b42d-00cf4fc964ff", null, "staff")]
    [InlineData("6f9619ff-8b86-d011-b42d-00cf4fc964ff", null, "customer")]
    [InlineData("6f9619ff-8b86-d011-b42d-00cf4fc964ff", "", "staff")]
    [InlineData("6f9619ff-8b86-d011-b42d-00cf4fc964ff", " ", "platform")]
    public void MalformedIdentityIsRejectedWithReason(string? subject, string? tenant, string? userType)
    {
        var principal = Principal(subject, tenant, userType);

        Assert.False(CurrentUser.TryCreate(principal, out var user, out var reason));
        Assert.Null(user);
        Assert.False(string.IsNullOrWhiteSpace(reason));
    }

    private static ClaimsPrincipal Principal(string? subject, string? tenant, string? userType)
    {
        var claims = new List<Claim>();
        if (subject is not null)
        {
            claims.Add(new Claim(AppClaimTypes.Subject, subject));
        }

        if (tenant is not null)
        {
            claims.Add(new Claim(AppClaimTypes.TenantId, tenant));
        }

        if (userType is not null)
        {
            claims.Add(new Claim(AppClaimTypes.UserType, userType));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }
}
