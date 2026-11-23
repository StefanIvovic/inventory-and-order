using BuildingBlocks.Core.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.EntityFrameworkCore.Tests;

public sealed class Order : ITenantOwned
{
    private Order()
    {
    }

    public Guid Id { get; private set; }

    public string Number { get; private set; } = string.Empty;

    public string TenantId { get; private set; } = string.Empty;

    public List<OrderLine> Lines { get; private set; } = [];

    public static Order Create(string number, string tenantId = "") =>
        new() { Id = Guid.NewGuid(), Number = number, TenantId = tenantId };

    public Order AddLine(string sku)
    {
        Lines.Add(OrderLine.Create(sku));
        return this;
    }
}

public sealed class OrderLine : ITenantOwned
{
    private OrderLine()
    {
    }

    public Guid Id { get; private set; }

    public Guid OrderId { get; private set; }

    public string Sku { get; private set; } = string.Empty;

    public string TenantId { get; private set; } = string.Empty;

    public static OrderLine Create(string sku) => new() { Id = Guid.NewGuid(), Sku = sku };
}

public sealed class Country
{
    public int Id { get; set; }

    public string Code { get; set; } = string.Empty;
}

public sealed class Note : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Text { get; set; } = string.Empty;

    public bool IsDeleted { get; set; }

    public string TenantId { get; private set; } = string.Empty;
}

public sealed class TestDbContext(DbContextOptions<TestDbContext> options, ITenantContext tenantContext)
    : TenantDbContext(options, tenantContext)
{
    public const string SoftDeleteFilter = "SoftDelete";

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<OrderLine> OrderLines => Set<OrderLine>();

    public DbSet<Country> Countries => Set<Country>();

    public DbSet<Note> Notes => Set<Note>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>().HasMany(o => o.Lines).WithOne().HasForeignKey(l => l.OrderId);
        modelBuilder.Entity<Note>().HasQueryFilter(SoftDeleteFilter, n => !n.IsDeleted);
        base.OnModelCreating(modelBuilder);
    }
}
