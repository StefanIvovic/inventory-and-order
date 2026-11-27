using BuildingBlocks.Core.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Product.Api.Persistence;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ProductDbContext>
{
    public ProductDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ProductDbContext>()
            .UseNpgsql("Host=localhost;Database=product_design")
            .Options;
        return new ProductDbContext(options, new TenantContext());
    }
}
