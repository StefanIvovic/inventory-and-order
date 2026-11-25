using Identity.Api.Users;

namespace Identity.Api.Protocol;

public static class ClientPolicies
{
    public const string Backoffice = "backoffice-bff";
    public const string Storefront = "storefront-bff";
    public const string TenantParameter = "tenant";

    private static readonly Dictionary<string, (IReadOnlySet<string> UserTypes, bool RequiresTenant)> Policies = new(StringComparer.Ordinal)
    {
        [Backoffice] = (new HashSet<string>(StringComparer.Ordinal) { UserTypes.Staff, UserTypes.Platform }, false),
        [Storefront] = (new HashSet<string>(StringComparer.Ordinal) { UserTypes.Customer }, true),
    };

    public static bool Accepts(string? clientId, string userType) =>
        clientId is not null && Policies.TryGetValue(clientId, out var policy) && policy.UserTypes.Contains(userType);

    public static bool RequiresTenant(string? clientId) =>
        clientId is not null && Policies.TryGetValue(clientId, out var policy) && policy.RequiresTenant;
}
