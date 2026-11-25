using BuildingBlocks.Lookups;
using Identity.Api.Lookups;
using Identity.Api.Protocol;
using Identity.Api.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;

namespace Identity.Api.Persistence;

public sealed class MigrationState
{
    private volatile bool applied;

    public bool Applied => applied;

    public void MarkApplied() => applied = true;
}

public sealed partial class StartupTasks(
    IServiceScopeFactory scopes,
    ProtocolSettings settings,
    ILogger<StartupTasks> logger) : IHostedService
{
    private const int MigrationAttempts = 10;
    private static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(10);

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var services = scope.ServiceProvider;

        await MigrateAsync(services.GetRequiredService<IdentityDbContext>(), cancellationToken);
        await LookupSeeder.SeedAsync(services.GetRequiredService<IdentityDbContext>(), IdentityLookups.Catalog, cancellationToken);
        await Scopes.RegisterAsync(services.GetRequiredService<IOpenIddictScopeManager>(), cancellationToken);
        await ClientRegistrar.RegisterAsync(services.GetRequiredService<IOpenIddictApplicationManager>(), settings, cancellationToken);
        await PlatformAdminSeeder.SeedAsync(
            services.GetRequiredService<UserManager<AppUser>>(),
            services.GetRequiredService<IdentityDbContext>(),
            settings.PlatformAdmin,
            services.GetRequiredService<TimeProvider>(),
            cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task MigrateAsync(IdentityDbContext db, CancellationToken cancellationToken)
    {
        var delay = TimeSpan.FromSeconds(1);
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await db.Database.MigrateAsync(cancellationToken);
                return;
            }
            catch (Exception exception) when (exception is not OperationCanceledException && attempt < MigrationAttempts)
            {
                LogRetry(logger, attempt, delay.TotalSeconds, exception.Message);
                await Task.Delay(delay, cancellationToken);
                delay = TimeSpan.FromTicks(Math.Min(delay.Ticks * 2, MaxDelay.Ticks));
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Identity database migration attempt {Attempt} failed, retrying in {DelaySeconds}s: {Reason}")]
    private static partial void LogRetry(ILogger logger, int attempt, double delaySeconds, string reason);
}
