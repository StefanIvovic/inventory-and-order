using Gateway.Tests.Fakes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace Gateway.Tests;

public sealed class GatewayApp : WebApplicationFactory<Program>
{
    public const string HttpsOrigin = "https://gateway.test";
    public const string BackofficeSecret = "backoffice-test-secret";
    public const string StorefrontSecret = "storefront-test-secret";

    private readonly Dictionary<string, string?> settings;

    private GatewayApp(Uri origin, FakeIdentity identity, SpaStub backoffice, SpaStub storefront, SpaStub product, Dictionary<string, string?> settings)
    {
        ProductApi = product;
        Origin = origin;
        Identity = identity;
        BackofficeSpa = backoffice;
        StorefrontSpa = storefront;
        this.settings = settings;
    }

    public Uri Origin { get; }

    public FakeIdentity Identity { get; }

    public SpaStub BackofficeSpa { get; }

    public SpaStub StorefrontSpa { get; }

    public SpaStub ProductApi { get; }

    public FakeTimeProvider Clock { get; } = new(DateTimeOffset.UtcNow);

    public static async Task<GatewayApp> StartAsync(string origin = HttpsOrigin, IReadOnlyDictionary<string, string?>? overrides = null)
    {
        var publicOrigin = new Uri(origin);
        var issuer = new Uri(publicOrigin, "/");
        var clients = new Dictionary<string, (string, string, string)>(StringComparer.Ordinal)
        {
            ["backoffice-bff"] = (BackofficeSecret, Url(publicOrigin, "/bff/backoffice/signin-oidc"), Url(publicOrigin, "/bff/backoffice/signout-callback-oidc")),
            ["storefront-bff"] = (StorefrontSecret, Url(publicOrigin, "/bff/storefront/signin-oidc"), Url(publicOrigin, "/bff/storefront/signout-callback-oidc")),
        };
        var identity = new FakeIdentity(issuer, clients);
        var backoffice = new SpaStub();
        var storefront = new SpaStub();
        var product = new SpaStub();
        await identity.StartAsync();
        await backoffice.StartAsync();
        await storefront.StartAsync();
        await product.StartAsync();

        var values = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Gateway:PublicOrigin"] = issuer.ToString(),
            ["Gateway:Identity:InternalAddress"] = identity.Address.ToString(),
            ["Gateway:Clients:backoffice-bff:Secret"] = BackofficeSecret,
            ["Gateway:Clients:storefront-bff:Secret"] = StorefrontSecret,
            ["ReverseProxy:Clusters:identity:Destinations:main:Address"] = identity.Address.ToString(),
            ["ReverseProxy:Clusters:backoffice-spa:Destinations:main:Address"] = backoffice.Address.ToString(),
            ["ReverseProxy:Clusters:storefront-spa:Destinations:main:Address"] = storefront.Address.ToString(),
            ["ReverseProxy:Clusters:product:Destinations:main:Address"] = product.Address.ToString(),
        };
        foreach (var (key, value) in overrides ?? new Dictionary<string, string?>())
        {
            values[key] = value;
        }

        var app = new GatewayApp(publicOrigin, identity, backoffice, storefront, product, values);
        _ = app.Server;
        return app;
    }

    public Browser CreateBrowser() =>
        new(CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false, BaseAddress = Origin }), Origin);

    public async Task<Browser> SignedInBackofficeAsync()
    {
        var browser = CreateBrowser();
        var landed = await browser.NavigateAsync("/bff/backoffice/login");
        Assert.Equal("/backoffice/", landed.Uri.AbsolutePath);
        return browser;
    }

    public async Task<Browser> SignedInStorefrontAsync(string slug, Browser? existing = null)
    {
        var browser = existing ?? CreateBrowser();
        var landed = await browser.NavigateAsync($"/bff/shop/{slug}/login");
        Assert.Equal($"/shop/{slug}/", landed.Uri.AbsolutePath);
        return browser;
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await Identity.DisposeAsync();
        await BackofficeSpa.DisposeAsync();
        await StorefrontSpa.DisposeAsync();
        await ProductApi.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.UseEnvironment("Testing");
        foreach (var (key, value) in settings)
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
        });
    }

    private static string Url(Uri origin, string path) => new Uri(origin, path).ToString();
}
