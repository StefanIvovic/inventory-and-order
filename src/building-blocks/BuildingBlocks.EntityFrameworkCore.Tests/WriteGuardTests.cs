using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.EntityFrameworkCore.Tests;

public sealed class WriteGuardTests : IDisposable
{
    private readonly SqliteFixture db = new();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InsertWithoutTenantIsStampedFromContext(bool async)
    {
        var order = Order.Create("A-1");
        await using (var context = db.NewContext("acme"))
        {
            context.Orders.Add(order);
            await Save(context, async);
        }

        await using var check = db.NewContext("acme");
        var stored = await check.Orders.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal("acme", stored.TenantId);
    }

    [Fact]
    public async Task InsertWithMatchingTenantIsAccepted()
    {
        await using (var context = db.NewContext("acme"))
        {
            context.Orders.Add(Order.Create("A-1", "acme"));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        Assert.Equal(1, CountAll());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InsertForDifferentTenantIsRejected(bool async)
    {
        await using (var context = db.NewContext("acme"))
        {
            context.Orders.Add(Order.Create("G-1", "globex"));
            await Assert.ThrowsAsync<TenantViolationException>(() => Save(context, async));
        }

        Assert.Equal(0, CountAll());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InsertWithoutTenantInContextIsRejected(bool async)
    {
        await using (var context = db.NewContext(null))
        {
            context.Orders.Add(Order.Create("X-1"));
            await Assert.ThrowsAsync<TenantViolationException>(() => Save(context, async));
        }

        Assert.Equal(0, CountAll());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ChangingTenantOfExistingRowIsRejected(bool async)
    {
        db.Seed("acme", Order.Create("A-1"));

        await using (var context = db.NewContext("acme"))
        {
            var order = await context.Orders.SingleAsync(TestContext.Current.CancellationToken);
            context.Entry(order).Property(o => o.TenantId).CurrentValue = "globex";
            await Assert.ThrowsAsync<TenantViolationException>(() => Save(context, async));
        }

        await using var check = db.NewContext("acme");
        Assert.Equal("acme", (await check.Orders.SingleAsync(TestContext.Current.CancellationToken)).TenantId);
    }

    [Fact]
    public async Task UpdatingOtherFieldsIsAccepted()
    {
        db.Seed("acme", new Note { Text = "before" });

        await using (var context = db.NewContext("acme"))
        {
            var note = await context.Notes.SingleAsync(TestContext.Current.CancellationToken);
            note.Text = "after";
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var check = db.NewContext("acme");
        Assert.Equal("after", (await check.Notes.SingleAsync(TestContext.Current.CancellationToken)).Text);
    }

    [Fact]
    public async Task DeletingIsAccepted()
    {
        db.Seed("acme", Order.Create("A-1"));

        await using (var context = db.NewContext("acme"))
        {
            context.Orders.Remove(await context.Orders.SingleAsync(TestContext.Current.CancellationToken));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        Assert.Equal(0, CountAll());
    }

    [Fact]
    public async Task MixedBatchWithOneInvalidEntityPersistsNothing()
    {
        await using (var context = db.NewContext("acme"))
        {
            context.Orders.AddRange(Order.Create("A-1"), Order.Create("A-2", "acme"), Order.Create("G-1", "globex"));
            context.Notes.Add(new Note { Text = "n" });
            await Assert.ThrowsAsync<TenantViolationException>(() => context.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        Assert.Equal(0, CountAll());
        await using var check = db.NewContext("acme");
        Assert.Equal(0, await check.Notes.AcrossAllTenants().IgnoreQueryFilters().CountAsync(TestContext.Current.CancellationToken));
    }

    public void Dispose() => db.Dispose();

    private static Task<int> Save(TestDbContext context, bool async) =>
        async ? context.SaveChangesAsync(TestContext.Current.CancellationToken) : Task.Run(() => context.SaveChanges());

    private int CountAll()
    {
        using var context = db.NewContext(null);
        return context.Orders.AcrossAllTenants().Count();
    }
}
