# Contract: BuildingBlocks.AspNetCore public API

```csharp
namespace BuildingBlocks.AspNetCore;
public static class BuildingBlocksExtensions
{
    public static IServiceCollection AddBuildingBlocks(this IServiceCollection services);
    public static IApplicationBuilder UseBuildingBlocks(this IApplicationBuilder app);
}
```

Service usage (SC-002):

```csharp
builder.Services.AddBuildingBlocks();
app.UseAuthentication();
app.UseBuildingBlocks();
```

## What `AddBuildingBlocks()` registers

- `AddProblemDetails()` with `traceId` customization
- `BuildingBlocksExceptionHandler` as `IExceptionHandler`
- an `IStartupFilter` that runs `UseExceptionHandler()` first in the pipeline
- `HttpContextAccessor`
- scoped `TenantContext`, registered both as itself and as `ITenantContext`
- scoped `ICurrentUser` → `HttpCurrentUser`

## What `UseBuildingBlocks()` adds

`IdentityMiddleware`. For an authenticated principal it:
- runs `CurrentUser.TryCreate`, and on failure returns 401 problem details (`code: invalid-identity`)
- stores the result in `HttpContext.Features`
- calls `TenantContext.Set` when a tenant is present
