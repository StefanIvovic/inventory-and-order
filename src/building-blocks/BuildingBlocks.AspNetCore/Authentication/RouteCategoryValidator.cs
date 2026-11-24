using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;

namespace BuildingBlocks.AspNetCore.Authentication;

internal sealed class RouteCategoryValidator(EndpointDataSource endpoints) : IHostedLifecycleService
{
    public Task StartedAsync(CancellationToken cancellationToken)
    {
        foreach (var endpoint in endpoints.Endpoints)
        {
            _ = RouteCategories.Of(endpoint);
        }

        return Task.CompletedTask;
    }

    public Task StartingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
