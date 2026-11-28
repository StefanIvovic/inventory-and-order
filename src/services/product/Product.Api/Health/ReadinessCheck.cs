using Microsoft.Extensions.Diagnostics.HealthChecks;
using Product.Api.Persistence;

namespace Product.Api.Health;

public sealed class ReadinessCheck(MigrationState migrations, IServiceScopeFactory scopes) : IHealthCheck
{
    public const string Tag = "ready";

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!migrations.Applied)
        {
            return HealthCheckResult.Unhealthy("Database migrations are not applied yet.");
        }

        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ProductDbContext>();
        return await db.Database.CanConnectAsync(cancellationToken)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("The database is unreachable.");
    }
}
