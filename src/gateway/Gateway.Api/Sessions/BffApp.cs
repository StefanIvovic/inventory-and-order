namespace Gateway.Api.Sessions;

public sealed class BffApp
{
    public const string SecureCookiePrefix = "__Host-Http-";

    public static readonly BffApp Backoffice = new("backoffice", "backoffice-bff", "bff-backoffice");
    public static readonly BffApp Storefront = new("storefront", "storefront-bff", "bff-storefront");

    private BffApp(string name, string clientId, string cookieBaseName)
    {
        Name = name;
        ClientId = clientId;
        CookieBaseName = cookieBaseName;
    }

    public static IReadOnlyList<BffApp> All { get; } = [Backoffice, Storefront];

    public string Name { get; }

    public string ClientId { get; }

    public string CookieBaseName { get; }

    public string CookieScheme => Name;

    public string OidcScheme => Name + "-oidc";

    public string CallbackPath => $"/bff/{Name}/signin-oidc";

    public string SignedOutCallbackPath => $"/bff/{Name}/signout-callback-oidc";

    public bool IsStorefront => ReferenceEquals(this, Storefront);

    public static BffApp? FromHeader(string? value) => value switch
    {
        "backoffice" => Backoffice,
        "storefront" => Storefront,
        _ => null,
    };

    public string CookieName(bool https) => https ? SecureCookiePrefix + CookieBaseName : CookieBaseName;

    public string StartPage(string? slug) => IsStorefront ? (slug is null ? "/shop/" : $"/shop/{slug}/") : "/backoffice/";

    public string BffPrefix(string? slug) => IsStorefront ? $"/bff/shop/{slug}" : "/bff/backoffice";
}
