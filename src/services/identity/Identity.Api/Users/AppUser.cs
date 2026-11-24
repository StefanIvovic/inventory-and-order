using Microsoft.AspNetCore.Identity;

namespace Identity.Api.Users;

public sealed class AppUser : IdentityUser<Guid>
{
    private AppUser()
    {
    }

    public string UserType { get; private set; } = string.Empty;

    public string? TenantId { get; private set; }

    public static AppUser Platform(string email) => new()
    {
        Id = Guid.NewGuid(),
        UserName = email,
        Email = email,
        EmailConfirmed = true,
        UserType = UserTypes.Platform,
    };

    public static AppUser ForTenant(string email, string tenantId, string userType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        if (userType is not (UserTypes.Staff or UserTypes.Customer))
        {
            throw new ArgumentException("A tenant user must be staff or customer.", nameof(userType));
        }

        return new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            UserType = userType,
            TenantId = tenantId,
        };
    }
}
