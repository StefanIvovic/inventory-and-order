using Microsoft.EntityFrameworkCore;

namespace Identity.Api.Persistence;

public sealed class MigrationState
{
    private volatile bool applied;

    public bool Applied => applied;

    public void MarkApplied() => applied = true;
}

public sealed partial class MigrationRunner(
    IServiceScopeFactory scopes,
    MigrationState state,
    ILogger<MigrationRunner> logger) : BackgroundService
{
    private static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var delay = TimeSpan.FromSeconds(1);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
                await db.Database.MigrateAsync(stoppingToken);
                state.MarkApplied();
                LogApplied(logger);
                return;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                LogRetry(logger, delay.TotalSeconds, exception.Message);
                await Task.Delay(delay, stoppingToken);
                delay = TimeSpan.FromTicks(Math.Min(delay.Ticks * 2, MaxDelay.Ticks));
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Identity database migrations applied")]
    private static partial void LogApplied(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Identity database migration failed, retrying in {DelaySeconds}s: {Reason}")]
    private static partial void LogRetry(ILogger logger, double delaySeconds, string reason);
}
