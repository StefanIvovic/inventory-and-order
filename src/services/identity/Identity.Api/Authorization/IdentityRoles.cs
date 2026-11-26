namespace Identity.Api.Authorization;

public static class IdentityRoles
{
    public const string Administrator = "identity.administrator";
    public const string Viewer = "identity.viewer";
    public const string PlatformAdministrator = "identity.platform-administrator";

    public static IReadOnlyDictionary<string, IReadOnlyList<string>> Tenant { get; } =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            [Administrator] = [IdentityPermissions.UsersView, IdentityPermissions.UsersManage, IdentityPermissions.RolesView, IdentityPermissions.RolesAssign],
            [Viewer] = [IdentityPermissions.UsersView, IdentityPermissions.RolesView],
        };

    public static IReadOnlyDictionary<string, IReadOnlyList<string>> Platform { get; } =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            [PlatformAdministrator] = [IdentityPermissions.TenantsView, IdentityPermissions.TenantsManage],
        };

    public static IReadOnlyDictionary<string, IReadOnlyList<string>> All { get; } =
        Tenant.Concat(Platform).ToDictionary(role => role.Key, role => role.Value, StringComparer.Ordinal);

    public static IReadOnlyDictionary<string, string> DisplayNames { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [Administrator] = "Administrator",
            [Viewer] = "Viewer",
            [PlatformAdministrator] = "Platform administrator",
        };

    public static IEnumerable<string> PermissionsOf(string role) =>
        All.TryGetValue(role, out var permissions) ? permissions : [];
}
