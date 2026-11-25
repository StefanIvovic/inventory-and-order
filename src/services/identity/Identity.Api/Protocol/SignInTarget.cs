using Identity.Api.Connect;
using Microsoft.AspNetCore.WebUtilities;

namespace Identity.Api.Protocol;

public sealed record SignInTarget(string? ClientId, string? TenantSlug)
{
    public bool IsCustomer => TenantSlug is not null;

    public static SignInTarget From(string? returnUrl)
    {
        if (string.IsNullOrEmpty(returnUrl) || !returnUrl.StartsWith('/') || returnUrl.StartsWith("//", StringComparison.Ordinal))
        {
            return new SignInTarget(null, null);
        }

        var queryStart = returnUrl.IndexOf('?', StringComparison.Ordinal);
        var path = queryStart < 0 ? returnUrl : returnUrl[..queryStart];
        if (!string.Equals(path, ConnectEndpoints.AuthorizePath, StringComparison.OrdinalIgnoreCase) || queryStart < 0)
        {
            return new SignInTarget(null, null);
        }

        var query = QueryHelpers.ParseQuery(returnUrl[queryStart..]);
        var clientId = query.TryGetValue("client_id", out var client) ? client.ToString() : null;
        var tenant = query.TryGetValue(ClientPolicies.TenantParameter, out var slug) ? slug.ToString() : null;
        return ClientPolicies.RequiresTenant(clientId) && !string.IsNullOrWhiteSpace(tenant)
            ? new SignInTarget(clientId, tenant)
            : new SignInTarget(clientId, null);
    }
}
