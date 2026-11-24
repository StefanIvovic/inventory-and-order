using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace BuildingBlocks.AspNetCore.Authentication;

public enum RouteCategory
{
    Tenant,
    Platform,
    AllowListed,
}

public static class RouteCategories
{
    public static RouteCategory Of(Endpoint endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        var allowListed = endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null;
        var platform = IsPlatform(endpoint);
        if (allowListed && platform)
        {
            throw new InvalidOperationException(
                $"Endpoint '{endpoint.DisplayName}' is both allow-listed and platform-only. Choose one.");
        }

        return allowListed ? RouteCategory.AllowListed : platform ? RouteCategory.Platform : RouteCategory.Tenant;
    }

    private static bool IsPlatform(Endpoint endpoint) =>
        endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>()
            .Any(data => string.Equals(data.Policy, AuthorizationPolicies.Platform, StringComparison.Ordinal));
}
