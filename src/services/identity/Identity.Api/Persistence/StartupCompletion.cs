using Identity.Api.Protocol;
using Identity.Api.Tenants;

namespace Identity.Api.Persistence;

public sealed partial class StartupCompletion(
    IServiceScopeFactory scopes,
    MigrationState state,
    ProtocolSettings settings,
    ILogger<StartupCompletion> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        if (await DemoTenantSeeder.SeedAsync(scope.ServiceProvider, settings.DemoTenant, cancellationToken))
        {
            LogDemoTenant(logger, settings.DemoTenant.Slug!);
        }

        state.MarkApplied();
        LogReady(logger);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Information, Message = "Demo tenant {Slug} created")]
    private static partial void LogDemoTenant(ILogger logger, string slug);

    [LoggerMessage(Level = LogLevel.Information, Message = "Identity is ready: migrations, scopes, clients, platform admin and demo tenant are in place")]
    private static partial void LogReady(ILogger logger);
}
