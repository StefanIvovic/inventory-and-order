using BuildingBlocks.AspNetCore;
using BuildingBlocks.AspNetCore.Authentication;
using BuildingBlocks.Messaging;
using BuildingBlocks.Validation;
using Identity.Contracts;
using Identity.Api.Authorization;
using Identity.Api.Connect;
using Identity.Api.Health;
using Identity.Api.Persistence;
using Identity.Api.Protocol;
using Identity.Api.Tenants;
using Identity.Api.Users;
using JasperFx;
using JasperFx.CodeGeneration;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Validation.AspNetCore;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.Http;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("Identity")
    ?? throw new InvalidOperationException("ConnectionStrings:Identity is required.");
var rabbitMqUri = new Uri(builder.Configuration["RabbitMq:Uri"]
    ?? throw new InvalidOperationException("RabbitMq:Uri is required."));
var protocol = ProtocolSettings.Read(builder.Configuration);

builder.Services.AddSingleton(protocol);
builder.Services.AddBuildingBlocks();
builder.Services.AddBuildingBlocksPermissions<IdentityPermissionProvider>();
builder.Services.AddDbContextWithWolverineIntegration<IdentityDbContext>(options =>
{
    options.UseNpgsql(connectionString);
    options.UseOpenIddict<Guid>();
});

builder.Services.AddSingleton<MigrationState>();
builder.Services.AddHostedService<StartupTasks>();

builder.Services.AddDataProtection()
    .SetApplicationName("identity")
    .PersistKeysToDbContext<IdentityDbContext>();

builder.Services
    .AddIdentityCore<AppUser>(options =>
    {
        options.Password.RequiredLength = 8;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
        options.Lockout.AllowedForNewUsers = true;
        options.User.RequireUniqueEmail = false;
        options.User.AllowedUserNameCharacters = string.Empty;
    })
    .AddUserStore<Microsoft.AspNetCore.Identity.EntityFrameworkCore.UserOnlyStore<AppUser, IdentityDbContext, Guid>>()
    .AddSignInManager<AppSignInManager>();
builder.Services.AddScoped<HandlerUserManager>();
builder.Services.AddScoped<TenantCreation>();
builder.Services.AddSingleton<BuildingBlocks.AspNetCore.Errors.IExceptionProblemMapper, UniqueViolationMapper>();

builder.Services
    .AddAuthentication(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)
    .AddIdentityCookies();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "identity.session";
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = protocol.RequireHttps ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
    options.LoginPath = "/account/login";
    options.ExpireTimeSpan = TimeSpan.FromHours(12);
    options.SlidingExpiration = true;
});
builder.Services.AddAntiforgery(options =>
    options.Cookie.SecurePolicy = protocol.RequireHttps ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest);

builder.Services.AddOpenIddict()
    .AddCore(core => core.UseEntityFrameworkCore().UseDbContext<IdentityDbContext>().ReplaceDefaultEntities<Guid>())
    .AddServer(server =>
    {
        server.SetIssuer(new Uri(protocol.Issuer, UriKind.Absolute))
            .SetAuthorizationEndpointUris(ConnectEndpoints.AuthorizePath)
            .SetTokenEndpointUris(ConnectEndpoints.TokenPath)
            .SetEndSessionEndpointUris(ConnectEndpoints.EndSessionPath)
            .SetJsonWebKeySetEndpointUris("/.well-known/jwks")
            .AllowAuthorizationCodeFlow()
            .AllowRefreshTokenFlow()
            .RequireProofKeyForCodeExchange()
            .SetAccessTokenLifetime(TimeSpan.FromMinutes(15))
            .SetRefreshTokenLifetime(TimeSpan.FromHours(12))
            .SetRefreshTokenReuseLeeway(null)
            .DisableAccessTokenEncryption()
            .RegisterScopes(["openid", "offline_access", .. Scopes.Api]);
        server.Configure(options => options.CodeChallengeMethods.Remove(OpenIddict.Abstractions.OpenIddictConstants.CodeChallengeMethods.Plain));
        SigningKeys.Configure(server, protocol.Keys, builder.Environment);

        var aspNetCore = server.UseAspNetCore()
            .EnableAuthorizationEndpointPassthrough()
            .EnableTokenEndpointPassthrough()
            .EnableEndSessionEndpointPassthrough();
        if (!protocol.RequireHttps)
        {
            aspNetCore.DisableTransportSecurityRequirement();
        }
    })
    .AddValidation(validation =>
    {
        validation.UseLocalServer();
        validation.AddAudiences("identity");
        validation.UseAspNetCore();
    });

builder.Services.AddBuildingBlocksRouteAuthorization(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
builder.Services.AddScoped<PrincipalFactory>();
builder.Services.AddHealthChecks().AddCheck<ReadinessCheck>(ReadinessCheck.Tag, tags: [ReadinessCheck.Tag]);
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AllowAnonymousToFolder("/Account");
    options.Conventions.AllowAnonymousToFolder("/Connect");
});
builder.Services.AddWolverineHttp();

builder.Host.UseWolverine(options =>
{
    options.ApplicationAssembly = typeof(Program).Assembly;
    options.UseBuildingBlocksMessaging(new MessagingSettings("identity", connectionString, rabbitMqUri));
    options.UseBuildingBlocksPermissionInvalidation();
    options.UseBuildingBlocksValidation();
    options.PublishEvent<TenantCreated>();
    options.PublishEvent<UserRegistered>();
    options.PublishEvent<UserDisabled>();
    options.CodeGeneration.AlwaysUseServiceLocationFor<HandlerUserManager>();
    options.CodeGeneration.AlwaysUseServiceLocationFor<TenantCreation>();
#if DEBUG
    options.CodeGeneration.TypeLoadMode = TypeLoadMode.Dynamic;
#else
    options.CodeGeneration.TypeLoadMode = TypeLoadMode.Static;
#endif
});

builder.Services.AddHostedService<StartupCompletion>();

var app = builder.Build();
app.UseStaticFiles();
app.UseBuildingBlocksAuthentication();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains(ReadinessCheck.Tag) }).AllowAnonymous();
app.MapConnectEndpoints();
app.MapRazorPages();
app.MapWolverineEndpoints();

return await app.RunJasperFxCommands(args);

public partial class Program;
