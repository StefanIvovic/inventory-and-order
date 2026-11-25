using Microsoft.AspNetCore.Identity;

namespace Identity.Api.Users;

public sealed class AppUser : IdentityUser<Guid>
{
    private AppUser()
    {
    }

    public string UserType { get; private set; } = string.Empty;

    public string? TenantId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? DisabledAt { get; private set; }

    public bool IsDisabled => DisabledAt is not null;

    public static AppUser Platform(string email, DateTimeOffset createdAt) => new()
    {
        Id = Guid.NewGuid(),
        UserName = email,
        Email = email,
        EmailConfirmed = true,
        UserType = UserTypes.Platform,
        CreatedAt = createdAt,
    };

    public static AppUser ForTenant(string email, string tenantId, string userType, DateTimeOffset createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        if (userType is not (UserTypes.Staff or UserTypes.Customer))
        {
            throw new ArgumentException("A tenant user must be staff or customer.", nameof(userType));
        }

        return new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = userType == UserTypes.Customer ? CustomerUserName(tenantId, email) : email,
            Email = email,
            EmailConfirmed = true,
            UserType = userType,
            TenantId = tenantId,
            CreatedAt = createdAt,
        };
    }

    public static string CustomerUserName(string tenantId, string email) => $"{tenantId}:{email}";

    public bool Disable(DateTimeOffset now)
    {
        if (IsDisabled)
        {
            return false;
        }

        DisabledAt = now;
        return true;
    }
}
