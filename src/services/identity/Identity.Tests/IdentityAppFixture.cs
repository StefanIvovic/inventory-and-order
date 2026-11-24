using BuildingBlocks.Core.Identity;
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
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Server;
using Wolverine;
using Wolverine.Tracking;
using System.Net.Http.Json;

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
    public const string TenantAdminPassword = "Tenant#Pass1";

    private readonly Dictionary<string, string?> settings;
    private readonly bool testSigningKey;

    private IdentityApp(Dictionary<string, string?> settings, bool testSigningKey)
    {
        this.settings = settings;
        this.testSigningKey = testSigningKey;
    }

    public static async Task<IdentityApp> StartAsync(
        ContainersFixture containers,
        string? connectionString = null,
        IReadOnlyDictionary<string, string?>? overrides = null,
        bool testSigningKey = true)
    {
        ArgumentNullException.ThrowIfNull(containers);
        var database = connectionString ?? await containers.Postgres.CreateDatabaseAsync(TestContext.Current.CancellationToken);
        var values = DefaultSettings(database, containers.RabbitMq.ConnectionString);
        foreach (var (key, value) in overrides ?? new Dictionary<string, string?>())
        {
            values[key] = value;
        }

        var app = new IdentityApp(values, testSigningKey);
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
        var user = AppUser.ForTenant(email, tenantId, userType, DateTimeOffset.UtcNow);
        var result = await users.CreateAsync(user, password);
        Assert.True(result.Succeeded, string.Join(" ", result.Errors.Select(e => e.Description)));
        return user;
    }

    public async Task<string> PlatformTokenAsync()
    {
        await WaitUntilMigratedAsync();
        var admin = await FindUserAsync(AdminEmail);
        return TestTokens.Platform(admin.Id, Audience);
    }

    public static string StaffToken(Guid userId, string tenantId) => TestTokens.For(userId, tenantId, UserType.Staff, Audience);

    public async Task<CreatedTenant> CreateTenantAsync(string slug, string adminEmail = "", string adminPassword = TenantAdminPassword)
    {
        using var client = CreateClient();
        var email = string.IsNullOrEmpty(adminEmail) ? $"admin@{slug}.test" : adminEmail;
        var response = await client.PostAsync("/tenants", new { slug, name = slug + " Ltd", adminEmail = email, adminPassword }, await PlatformTokenAsync());
        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<CreatedTenant>(TestContext.Current.CancellationToken);
        Assert.NotNull(created);
        return created with { AdminEmail = email };
    }

    public async Task<AppUser> FindUserAsync(string email)
    {
        await using var scope = Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = await users.FindByEmailAsync(email);
        Assert.NotNull(user);
        return user;
    }

    public async Task<ITrackedSession> TrackAsync(Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return await Services.GetRequiredService<IHost>()
            .TrackActivity()
            .IncludeExternalTransports()
            .DoNotAssertOnExceptionsDetected()
            .Timeout(TimeSpan.FromSeconds(60))
            .ExecuteAndWaitAsync((IMessageContext _) => action());
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
        if (testSigningKey)
        {
            builder.ConfigureTestServices(services =>
                services.PostConfigure<OpenIddictServerOptions>(options =>
                    options.SigningCredentials.Add(new SigningCredentials(TestTokens.Key, SecurityAlgorithms.RsaSha256))));
        }
    }
}

public sealed record CreatedTenant(string Id, string Slug, Guid AdminUserId)
{
    public string AdminEmail { get; init; } = string.Empty;
}
