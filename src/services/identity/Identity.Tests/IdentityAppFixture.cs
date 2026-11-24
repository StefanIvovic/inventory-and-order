using BuildingBlocks.Core.Tenancy;
using BuildingBlocks.Testing;
using Identity.Api.Persistence;
using Identity.Api.Users;
using JasperFx.CommandLine;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Server;

[assembly: AssemblyFixture(typeof(Identity.Tests.ContainersFixture))]

namespace Identity.Tests;

public sealed class ContainersFixture : IAsyncLifetime
{
    public PostgresFixture Postgres { get; } = new();

    public RabbitMqFixture RabbitMq { get; } = new();

    public async ValueTask InitializeAsync()
    {
        JasperFxEnvironment.AutoStartHost = true;
        await Postgres.InitializeAsync();
        await RabbitMq.InitializeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await RabbitMq.DisposeAsync();
        await Postgres.DisposeAsync();
    }
}

public sealed class IdentityApp : WebApplicationFactory<Program>
{
    public const string Audience = "identity";
    public const string Client = "backoffice-bff";
    public const string ClientSecret = "backoffice-test-secret";
    public const string RedirectUri = "https://gateway.test/bff/backoffice/signin-oidc";
    public const string PostLogoutRedirectUri = "https://gateway.test/backoffice/";
    public const string AdminEmail = "admin@platform.test";
    public const string AdminPassword = "Admin#Pass1";

    private readonly Dictionary<string, string?> settings;

    private IdentityApp(Dictionary<string, string?> settings)
    {
        this.settings = settings;
    }

    public static async Task<IdentityApp> StartAsync(
        ContainersFixture containers,
        string? connectionString = null,
        IReadOnlyDictionary<string, string?>? overrides = null)
    {
        ArgumentNullException.ThrowIfNull(containers);
        var database = connectionString ?? await containers.Postgres.CreateDatabaseAsync(TestContext.Current.CancellationToken);
        var values = DefaultSettings(database, containers.RabbitMq.ConnectionString);
        foreach (var (key, value) in overrides ?? new Dictionary<string, string?>())
        {
            values[key] = value;
        }

        var app = new IdentityApp(values);
        _ = app.Server;
        return app;
    }

    public static Dictionary<string, string?> DefaultSettings(string database, string rabbitMq) => new(StringComparer.Ordinal)
    {
        ["ConnectionStrings:Identity"] = database,
        ["RabbitMq:Uri"] = rabbitMq,
        ["Identity:Issuer"] = TestTokens.Issuer,
        ["Identity:RequireHttps"] = "false",
        ["Identity:Keys:Mode"] = "Ephemeral",
        ["Identity:Clients:backoffice-bff:Secret"] = ClientSecret,
        ["Identity:Clients:backoffice-bff:RedirectUri"] = RedirectUri,
        ["Identity:Clients:backoffice-bff:PostLogoutRedirectUri"] = PostLogoutRedirectUri,
        ["Identity:Clients:storefront-bff:Secret"] = "storefront-test-secret",
        ["Identity:Clients:storefront-bff:RedirectUri"] = "https://gateway.test/bff/storefront/signin-oidc",
        ["Identity:Clients:storefront-bff:PostLogoutRedirectUri"] = "https://gateway.test/shop/",
        ["Identity:PlatformAdmin:Email"] = AdminEmail,
        ["Identity:PlatformAdmin:Password"] = AdminPassword,
    };

    public HttpClient CreateBrowser() =>
        CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

    public async Task WaitUntilMigratedAsync()
    {
        var state = Services.GetRequiredService<MigrationState>();
        var deadline = DateTimeOffset.UtcNow.AddSeconds(60);
        while (!state.Applied && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(100, TestContext.Current.CancellationToken);
        }

        Assert.True(state.Applied, "Identity startup tasks did not finish within 60 seconds.");
    }

    public async Task AssignAsync(string tenantId, Guid userId, string role)
    {
        await WaitUntilMigratedAsync();
        await using var scope = Services.CreateAsyncScope();
        var tenant = scope.ServiceProvider.GetRequiredService<TenantContext>();
        tenant.Set(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        db.RoleAssignments.Add(RoleAssignment.Create(userId, role, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async Task<AppUser> CreateUserAsync(string email, string password, string tenantId, string userType = UserTypes.Staff)
    {
        await WaitUntilMigratedAsync();
        await using var scope = Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = AppUser.ForTenant(email, tenantId, userType);
        var result = await users.CreateAsync(user, password);
        Assert.True(result.Succeeded, string.Join(" ", result.Errors.Select(e => e.Description)));
        return user;
    }

    public async Task WithUsersAsync(Func<UserManager<AppUser>, Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        await using var scope = Services.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>());
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        foreach (var (key, value) in settings)
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureTestServices(services =>
            services.PostConfigure<OpenIddictServerOptions>(options =>
                options.SigningCredentials.Add(new SigningCredentials(TestTokens.Key, SecurityAlgorithms.RsaSha256))));
    }
}
