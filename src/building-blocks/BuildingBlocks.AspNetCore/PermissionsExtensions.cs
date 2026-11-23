using BuildingBlocks.AspNetCore.Handlers;
using BuildingBlocks.AspNetCore.Permissions;
using BuildingBlocks.Core.Handlers;
using BuildingBlocks.Core.Permissions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BuildingBlocks.AspNetCore;

public static class PermissionsExtensions
{
    public static IServiceCollection AddBuildingBlocksPermissions<TProvider>(
        this IServiceCollection services,
        Action<PermissionOptions>? configure = null)
        where TProvider : class, IPermissionProvider
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<PermissionOptions>()
            .Configure(options => configure?.Invoke(options))
            .Validate(options => options.CacheDuration > TimeSpan.Zero, "PermissionOptions.CacheDuration must be greater than zero.")
            .ValidateOnStart();

        services.AddMemoryCache();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<PermissionCache>();
        services.TryAddSingleton<IPermissionCache>(provider => provider.GetRequiredService<PermissionCache>());
        services.TryAddScoped<IPermissionProvider, TProvider>();
        services.TryAddScoped<IPermissionManager, PermissionManager>();
        services.TryAddScoped<IRequestContext, RequestContext>();

        return services;
    }
}
