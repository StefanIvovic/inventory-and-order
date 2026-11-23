using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;

namespace BuildingBlocks.Core.Identity;

public sealed record CurrentUser(Guid UserId, string? TenantId, UserType UserType)
{
    public static bool TryCreate(
        ClaimsPrincipal principal,
        [NotNullWhen(true)] out CurrentUser? user,
        [NotNullWhen(false)] out string? rejectionReason)
    {
        ArgumentNullException.ThrowIfNull(principal);
        user = null;

        if (!Guid.TryParse(principal.FindFirst(AppClaimTypes.Subject)?.Value, out var userId))
        {
            rejectionReason = $"claim '{AppClaimTypes.Subject}' is missing or not a valid identifier";
            return false;
        }

        var userTypeValue = principal.FindFirst(AppClaimTypes.UserType)?.Value;
        if (!Enum.TryParse<UserType>(userTypeValue, ignoreCase: true, out var userType) ||
            !Enum.IsDefined(userType) ||
            int.TryParse(userTypeValue, out _))
        {
            rejectionReason = $"claim '{AppClaimTypes.UserType}' is missing or unknown";
            return false;
        }

        var tenantClaim = principal.FindFirst(AppClaimTypes.TenantId);
        if (tenantClaim is not null && string.IsNullOrWhiteSpace(tenantClaim.Value))
        {
            rejectionReason = $"claim '{AppClaimTypes.TenantId}' is empty";
            return false;
        }

        if (tenantClaim is null && userType != UserType.Platform)
        {
            rejectionReason = $"claim '{AppClaimTypes.TenantId}' is required for {userType} users";
            return false;
        }

        user = new CurrentUser(userId, tenantClaim?.Value, userType);
        rejectionReason = null;
        return true;
    }
}
