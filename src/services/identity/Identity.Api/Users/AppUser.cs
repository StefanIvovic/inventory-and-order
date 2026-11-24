using Microsoft.AspNetCore.Identity;

namespace Identity.Api.Users;

public sealed class AppUser : IdentityUser<Guid>
{
    public string UserType { get; set; } = UserTypes.Staff;

    public string? TenantId { get; set; }

    public static AppUser Platform(string email) => new()
    {
        Id = Guid.NewGuid(),
        UserName = email,
        Email = email,
        EmailConfirmed = true,
        UserType = UserTypes.Platform,
    };

    public static AppUser ForTenant(string email, string tenantId, string userType) => new()
    {
        Id = Guid.NewGuid(),
        UserName = email,
        Email = email,
        EmailConfirmed = true,
        UserType = userType,
        TenantId = tenantId,
    };
}
