namespace Product.Api.Persistence;

public sealed partial class StartupCompletion(MigrationState state, ILogger<StartupCompletion> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        state.MarkApplied();
        LogReady(logger);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Information, Message = "Product is ready: migrations and message storage are in place")]
    private static partial void LogReady(ILogger logger);
}
