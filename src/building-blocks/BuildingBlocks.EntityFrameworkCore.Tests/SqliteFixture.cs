using BuildingBlocks.Core.Tenancy;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.EntityFrameworkCore.Tests;

public sealed class SqliteFixture : IDisposable
{
    private readonly SqliteConnection connection;
    private readonly DbContextOptions<TestDbContext> options;

    public SqliteFixture()
    {
        connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        options = new DbContextOptionsBuilder<TestDbContext>().UseSqlite(connection).Options;

        using var context = NewContext(null);
        context.Database.EnsureCreated();
    }

    public TestDbContext NewContext(string? tenant)
    {
        var tenantContext = new TenantContext();
        if (tenant is not null)
        {
            tenantContext.Set(tenant);
        }

        return new TestDbContext(options, tenantContext);
    }

    public void Seed(string tenant, params object[] entities)
    {
        using var context = NewContext(tenant);
        context.AddRange(entities);
        context.SaveChanges();
    }

    public void SeedUnfiltered(params object[] entities)
    {
        using var context = NewContext(null);
        context.AddRange(entities);
        context.SaveChanges();
    }

    public void Dispose() => connection.Dispose();
}
