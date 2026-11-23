# Contract: Tenancy API

```csharp
namespace BuildingBlocks.Core.Tenancy;
public interface ITenantOwned { string TenantId { get; } }

namespace BuildingBlocks.EntityFrameworkCore;
public static class TenantFilter { public const string Name = "Tenant"; public const int TenantIdMaxLength = 64; }
public abstract class TenantDbContext : DbContext
{
    protected TenantDbContext(DbContextOptions options, ITenantContext tenantContext);
    protected string? CurrentTenantId { get; }
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder);
    protected override void OnModelCreating(ModelBuilder modelBuilder);
}
public sealed class TenantViolationException : InvalidOperationException { }
public static class TenantQueryableExtensions { public static IQueryable<T> AcrossAllTenants<T>(this IQueryable<T> source) where T : class; }
```

## Service usage

```csharp
public sealed class Order : ITenantOwned { public Guid Id { get; private set; } public string TenantId { get; private set; } = string.Empty; }

public sealed class OrderDbContext(DbContextOptions<OrderDbContext> options, ITenantContext tenant)
    : TenantDbContext(options, tenant)
{
    public DbSet<Order> Orders => Set<Order>();
}
```

Derived contexts that override `OnModelCreating` or `OnConfiguring` MUST call the base method.
