namespace Product.Api.Authorization;

public static class ProductPermissions
{
    public const string RolesView = "product.roles.view";

    public static IReadOnlyList<string> All { get; } = [RolesView];

    public static IReadOnlyDictionary<string, string> DisplayNames { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [RolesView] = "View roles",
        };
}
