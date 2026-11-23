using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.EntityFrameworkCore.Tests;

public sealed class QueryFilterTests : IDisposable
{
    private readonly SqliteFixture db = new();
    private readonly Order acmeOrder = Order.Create("A-1").AddLine("acme-sku");
    private readonly Order globexOrder = Order.Create("G-1").AddLine("globex-sku");

    public QueryFilterTests()
    {
        db.Seed("acme", acmeOrder, Order.Create("A-2"));
        db.Seed("globex", globexOrder);
        db.SeedUnfiltered(new Country { Id = 1, Code = "RS" }, new Country { Id = 2, Code = "DE" });
    }

    [Fact]
    public async Task ListReturnsOnlyCurrentTenant()
    {
        await using var context = db.NewContext("acme");

        var numbers = await context.Orders.Select(o => o.Number).OrderBy(n => n).ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["A-1", "A-2"], numbers);
    }

    [Fact]
    public async Task FindByOtherTenantsIdReturnsNothing()
    {
        await using var context = db.NewContext("acme");

        var found = await context.Orders.FindAsync([globexOrder.Id], TestContext.Current.CancellationToken);

        Assert.Null(found);
    }

    [Fact]
    public async Task CountIsScopedToCurrentTenant()
    {
        await using var context = db.NewContext("globex");

        Assert.Equal(1, await context.Orders.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, await context.OrderLines.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task IncludeAndJoinExcludeOtherTenantsRows()
    {
        await using var context = db.NewContext("acme");
        var cancellation = TestContext.Current.CancellationToken;

        var included = await context.Orders.Include(o => o.Lines).SelectMany(o => o.Lines).Select(l => l.Sku).ToListAsync(cancellation);
        var joined = await context.OrderLines
            .Join(context.Orders, l => l.OrderId, o => o.Id, (l, o) => l.Sku)
            .ToListAsync(cancellation);

        Assert.Equal(["acme-sku"], included);
        Assert.Equal(["acme-sku"], joined);
    }

    [Fact]
    public async Task ProjectionReturnsOnlyCurrentTenant()
    {
        await using var context = db.NewContext("acme");

        var projected = await context.Orders.Select(o => new { o.Number, Lines = o.Lines.Count }).ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, projected.Count);
        Assert.DoesNotContain(projected, p => p.Number.StartsWith('G'));
    }

    [Fact]
    public async Task NoTenantFailsClosed()
    {
        await using var context = db.NewContext(null);
        var cancellation = TestContext.Current.CancellationToken;

        Assert.Empty(await context.Orders.ToListAsync(cancellation));
        Assert.Empty(await context.OrderLines.ToListAsync(cancellation));
    }

    [Fact]
    public async Task NonTenantEntityIsNotFiltered()
    {
        await using var context = db.NewContext("acme");

        Assert.Equal(2, await context.Countries.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void GeneratedSqlFiltersByParameterizedTenant()
    {
        using var context = db.NewContext("acme");

        var sql = context.Orders.ToQueryString();

        Assert.Contains("\"TenantId\" = @", sql, StringComparison.Ordinal);
    }

    public void Dispose() => db.Dispose();
}
