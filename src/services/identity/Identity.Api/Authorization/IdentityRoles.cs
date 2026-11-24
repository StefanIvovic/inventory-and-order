namespace Identity.Api.Authorization;

public static class IdentityRoles
{
    public const string Administrator = "identity.administrator";

    public static IReadOnlyDictionary<string, IReadOnlyList<string>> All { get; } =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            [Administrator] = [IdentityPermissions.RolesView],
        };

    public static IEnumerable<string> PermissionsOf(string role) =>
        All.TryGetValue(role, out var permissions) ? permissions : [];
}
