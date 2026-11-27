using Microsoft.EntityFrameworkCore;

namespace Product.Api.Persistence;

public sealed class MigrationState
{
    private volatile bool applied;

    public bool Applied => applied;

    public void MarkApplied() => applied = true;
}

public sealed partial class StartupTasks(IServiceScopeFactory scopes, ILogger<StartupTasks> logger) : IHostedService
{
    private const int MigrationAttempts = 10;
    private static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(10);

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        await MigrateAsync(scope.ServiceProvider.GetRequiredService<ProductDbContext>(), cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task MigrateAsync(ProductDbContext db, CancellationToken cancellationToken)
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

    [LoggerMessage(Level = LogLevel.Warning, Message = "Product database migration attempt {Attempt} failed, retrying in {DelaySeconds}s: {Reason}")]
    private static partial void LogRetry(ILogger logger, int attempt, double delaySeconds, string reason);
}
