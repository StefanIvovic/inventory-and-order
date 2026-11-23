# Contract: Permissions API

```csharp
namespace BuildingBlocks.Core.Permissions;
public interface IPermissionManager
{
    Task<bool> HasAsync(string permission, CancellationToken cancellationToken = default);
    Task<bool> HasAllAsync(IEnumerable<string> permissions, CancellationToken cancellationToken = default);
    Task<bool> HasAnyAsync(IEnumerable<string> permissions, CancellationToken cancellationToken = default);
    Task EnsureAsync(string permission, CancellationToken cancellationToken = default);
    Task EnsureAllAsync(IEnumerable<string> permissions, CancellationToken cancellationToken = default);
    Task<IReadOnlySet<string>> GetGrantedAsync(CancellationToken cancellationToken = default);
}
public interface IPermissionProvider { Task<IReadOnlySet<string>> GetPermissionsAsync(ICurrentUser user, CancellationToken cancellationToken); }
public interface IPermissionCache { void Invalidate(string? tenantId, Guid userId); }
public sealed class PermissionDeniedException : Exception { public const string ErrorCode = "permission-denied"; public PermissionDeniedException(IEnumerable<string> missing); public IReadOnlyList<string> Missing { get; } public string Code { get; } }

namespace BuildingBlocks.Core.Handlers;
public interface IRequestContext { ICurrentUser User { get; } ITenantContext Tenant { get; } IPermissionManager Permissions { get; } TimeProvider Clock { get; } }
public abstract class CommandHandler<TDbContext>(IRequestContext context, TDbContext db) where TDbContext : class
{
    protected IRequestContext Context { get; }
    protected TDbContext Db { get; }
    protected ICurrentUser User { get; }
    protected IPermissionManager Permissions { get; }
    protected ITenantContext Tenant { get; }
    protected TimeProvider Clock { get; }
}

namespace BuildingBlocks.AspNetCore;
public sealed class PermissionOptions { public TimeSpan CacheDuration { get; set; } = TimeSpan.FromSeconds(60); }
public static class PermissionsExtensions
{
    public static IServiceCollection AddBuildingBlocksPermissions<TProvider>(this IServiceCollection services, Action<PermissionOptions>? configure = null)
        where TProvider : class, IPermissionProvider;
}
```

## 403 body

```json
{ "type": "https://tools.ietf.org/html/rfc9110#section-15.5.4", "title": "Forbidden", "status": 403,
  "detail": "You do not have the permissions required for this operation.",
  "code": "permission-denied", "missingPermissions": ["orders.cancel"], "traceId": "..." }
```

## Service usage

```csharp
builder.Services.AddBuildingBlocks();
builder.Services.AddBuildingBlocksPermissions<InventoryPermissionProvider>();

public sealed class TransferStockHandler(IRequestContext context, InventoryDbContext db)
    : CommandHandler<InventoryDbContext>(context, db)
{
    public async Task Handle(TransferStock command, CancellationToken ct)
    {
        await Permissions.EnsureAsync(InventoryPermissions.Transfer, ct);
    }
}
```
