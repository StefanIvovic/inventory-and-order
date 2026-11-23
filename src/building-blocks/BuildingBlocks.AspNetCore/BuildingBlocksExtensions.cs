using BuildingBlocks.AspNetCore.Errors;
using BuildingBlocks.AspNetCore.Identity;
using BuildingBlocks.Core.Identity;
using BuildingBlocks.Core.Tenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BuildingBlocks.AspNetCore;

public static class BuildingBlocksExtensions
{
    public static IServiceCollection AddBuildingBlocks(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddProblemDetails(ProblemDetailsSetup.Configure);
        services.AddExceptionHandler<BuildingBlocksExceptionHandler>();
        services.TryAddEnumerable(ServiceDescriptor.Transient<IStartupFilter, ExceptionHandlerStartupFilter>());
        services.AddHttpContextAccessor();
        services.TryAddScoped<TenantContext>();
        services.TryAddScoped<ITenantContext>(provider => provider.GetRequiredService<TenantContext>());
        services.TryAddScoped<ICurrentUser, HttpCurrentUser>();

        return services;
    }

    public static IApplicationBuilder UseBuildingBlocks(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.UseMiddleware<IdentityMiddleware>();
    }
}
