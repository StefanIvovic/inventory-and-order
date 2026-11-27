namespace Product.Api.Authorization;

public static class ProductRoles
{
    public const string Administrator = "product.administrator";
    public const string Viewer = "product.viewer";

    public static IReadOnlyDictionary<string, IReadOnlyList<string>> Tenant { get; } =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            [Administrator] = ProductPermissions.All,
            [Viewer] = [ProductPermissions.RolesView],
        };

    public static IReadOnlyDictionary<string, string> DisplayNames { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [Administrator] = "Administrator",
            [Viewer] = "Viewer",
        };

    public static IEnumerable<string> PermissionsOf(string role) =>
        Tenant.TryGetValue(role, out var permissions) ? permissions : [];
}
