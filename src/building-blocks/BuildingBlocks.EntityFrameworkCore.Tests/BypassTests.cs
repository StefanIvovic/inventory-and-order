using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.EntityFrameworkCore.Tests;

public sealed class BypassTests : IDisposable
{
    private readonly SqliteFixture db = new();

    public BypassTests()
    {
        db.Seed("acme", Order.Create("A-1"), new Note { Text = "acme-live" }, new Note { Text = "acme-deleted", IsDeleted = true });
        db.Seed("globex", Order.Create("G-1"), new Note { Text = "globex-live" });
    }

    [Fact]
    public async Task AcrossAllTenantsReturnsEveryTenant()
    {
        await using var context = db.NewContext("acme");

        var numbers = await context.Orders.AcrossAllTenants().Select(o => o.Number).OrderBy(n => n).ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["A-1", "G-1"], numbers);
    }

    [Fact]
    public async Task NextQueryWithoutBypassIsFilteredAgain()
    {
        await using var context = db.NewContext("acme");
        var cancellation = TestContext.Current.CancellationToken;

        _ = await context.Orders.AcrossAllTenants().ToListAsync(cancellation);
        var numbers = await context.Orders.Select(o => o.Number).ToListAsync(cancellation);

        Assert.Equal(["A-1"], numbers);
    }

    [Fact]
    public async Task BypassKeepsOtherNamedFilters()
    {
        await using var context = db.NewContext("acme");

        var texts = await context.Notes.AcrossAllTenants().Select(n => n.Text).OrderBy(t => t).ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["acme-live", "globex-live"], texts);
    }

    public void Dispose() => db.Dispose();
}
