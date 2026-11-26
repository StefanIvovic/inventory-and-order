using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace BuildingBlocks.AspNetCore.Authentication;

public enum RouteCategory
{
    Tenant,
    Platform,
    SignedIn,
    AllowListed,
}

public static class RouteCategories
{
    public static RouteCategory Of(Endpoint endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        var allowListed = endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null;
        var platform = HasPolicy(endpoint, AuthorizationPolicies.Platform);
        var signedIn = HasPolicy(endpoint, AuthorizationPolicies.SignedIn);
        if ((allowListed ? 1 : 0) + (platform ? 1 : 0) + (signedIn ? 1 : 0) > 1)
        {
            throw new InvalidOperationException(
                $"Endpoint '{endpoint.DisplayName}' combines route categories (allow-listed, platform-only, signed-in). Choose one.");
        }

        return allowListed ? RouteCategory.AllowListed
            : platform ? RouteCategory.Platform
            : signedIn ? RouteCategory.SignedIn
            : RouteCategory.Tenant;
    }

    private static bool HasPolicy(Endpoint endpoint, string policy) =>
        endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>()
            .Any(data => string.Equals(data.Policy, policy, StringComparison.Ordinal));
}
