using System.Reflection;
using BuildingBlocks.Core.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.EntityFrameworkCore;

public abstract class TenantDbContext : DbContext
{
    private static readonly MethodInfo ConfigureTenantOwnedMethod =
        typeof(TenantDbContext).GetMethod(nameof(ConfigureTenantOwned), BindingFlags.Instance | BindingFlags.NonPublic)!;

    private readonly ITenantContext tenantContext;

    protected TenantDbContext(DbContextOptions options, ITenantContext tenantContext)
        : base(options)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        this.tenantContext = tenantContext;
    }

    protected string? CurrentTenantId => tenantContext.TenantId;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);
        optionsBuilder.AddInterceptors(new TenantSaveChangesInterceptor(tenantContext));
        base.OnConfiguring(optionsBuilder);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        base.OnModelCreating(modelBuilder);

        var tenantOwnedTypes = modelBuilder.Model.GetEntityTypes()
            .Where(type => !type.IsOwned() && type.ClrType is { } clr && typeof(ITenantOwned).IsAssignableFrom(clr) && type.BaseType is null)
            .Select(type => type.ClrType)
            .ToList();

        foreach (var clrType in tenantOwnedTypes)
        {
            ConfigureTenantOwnedMethod.MakeGenericMethod(clrType).Invoke(this, [modelBuilder]);
        }
    }

    private void ConfigureTenantOwned<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ITenantOwned
    {
        var entity = modelBuilder.Entity<TEntity>();
        entity.Property(e => e.TenantId).IsRequired().HasMaxLength(TenantFilter.TenantIdMaxLength);
        entity.HasIndex(e => e.TenantId);
        entity.HasQueryFilter(TenantFilter.Name, e => e.TenantId == CurrentTenantId);
    }
}
