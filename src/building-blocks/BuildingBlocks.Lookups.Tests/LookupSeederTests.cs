using BuildingBlocks.Testing;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.Lookups.Tests;

public sealed class LookupSeederTests(PostgresFixture postgres)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task EmptyDatabaseGetsEveryDefinedRow()
    {
        var db = await LookupTestDb.CreateAsync(postgres);

        await SeedAsync(db, Catalogs.Colors());

        await using var context = db.Context();
        Assert.Equal([Catalogs.ColorId, Catalogs.SizeId], (await context.Set<LookupType>().OrderBy(t => t.Code).ToListAsync(Cancellation)).Select(t => t.Id));
        var values = await context.Set<LookupValue>().OrderBy(v => v.Code).ToListAsync(Cancellation);
        Assert.Equal(["blue", "green", "red", "small"], values.Select(v => v.Code));
        Assert.Equal(Catalogs.RedId, values.Single(v => v.Code == "red").Id);
        Assert.Equal(Catalogs.SizeId, values.Single(v => v.Code == "small").LookupTypeId);
    }

    [Fact]
    public async Task ChangedNameIsRestoredWithTheSameId()
    {
        var db = await LookupTestDb.CreateAsync(postgres);
        await SeedAsync(db, Catalogs.Colors());
        await db.ExecuteAsync($"UPDATE lookup_values SET \"Name\" = 'Scarlet' WHERE \"Id\" = '{Catalogs.RedId}'");

        await SeedAsync(db, Catalogs.Colors());

        await using var context = db.Context();
        var red = await context.Set<LookupValue>().SingleAsync(v => v.Code == "red", Cancellation);
        Assert.Equal(("Red", Catalogs.RedId), (red.Name, red.Id));
    }

    [Fact]
    public async Task ExtraRowIsLeftAlone()
    {
        var db = await LookupTestDb.CreateAsync(postgres);
        await SeedAsync(db, Catalogs.Colors());
        var extra = Guid.NewGuid();
        await db.ExecuteAsync($"INSERT INTO lookup_values (\"Id\", \"Code\", \"Name\", \"LookupTypeId\") VALUES ('{extra}', 'purple', 'Purple', '{Catalogs.ColorId}')");

        await SeedAsync(db, Catalogs.Colors());

        await using var context = db.Context();
        Assert.True(await context.Set<LookupValue>().AnyAsync(v => v.Id == extra && v.Name == "Purple", Cancellation));
    }

    [Fact]
    public async Task SecondRunChangesNothing()
    {
        var db = await LookupTestDb.CreateAsync(postgres);
        await SeedAsync(db, Catalogs.Colors());
        await using var context = db.Context();

        await LookupSeeder.SeedAsync(context, Catalogs.Colors(), Cancellation);

        Assert.Equal(6, await context.Set<LookupType>().CountAsync(Cancellation) + await context.Set<LookupValue>().CountAsync(Cancellation));
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task ConcurrentRunsBothSucceedWithOneRowEach()
    {
        var db = await LookupTestDb.CreateAsync(postgres);

        await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => SeedAsync(db, Catalogs.Many(300))));

        await using var context = db.Context();
        Assert.Equal(300, await context.Set<LookupValue>().CountAsync(Cancellation));
        Assert.Equal(1, await context.Set<LookupType>().CountAsync(Cancellation));
    }

    [Fact]
    public async Task DefinedCodeUnderAnotherIdFailsSeeding()
    {
        var db = await LookupTestDb.CreateAsync(postgres);
        await db.ExecuteAsync($"INSERT INTO lookup_types (\"Id\", \"Code\", \"Name\") VALUES ('{Guid.NewGuid()}', 'color', 'Colour')");

        var error = await Assert.ThrowsAsync<LookupSeedException>(() => SeedAsync(db, Catalogs.Colors()));

        Assert.Contains("different id", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RowDeletedDuringSeedingIsReportedAsMissing()
    {
        var db = await LookupTestDb.CreateAsync(postgres);
        await SeedAsync(db, Catalogs.Colors());
        await db.ExecuteAsync(
            $"CREATE FUNCTION drop_red() RETURNS trigger AS $$ BEGIN DELETE FROM lookup_values WHERE \"Id\" = '{Catalogs.RedId}'; RETURN NULL; END $$ LANGUAGE plpgsql;" +
            " CREATE TRIGGER drop_red AFTER UPDATE ON lookup_types FOR EACH ROW EXECUTE FUNCTION drop_red();" +
            $" UPDATE lookup_types SET \"Name\" = 'Colour' WHERE \"Id\" = '{Catalogs.ColorId}'");
        await db.ExecuteAsync($"INSERT INTO lookup_values (\"Id\", \"Code\", \"Name\", \"LookupTypeId\") VALUES ('{Catalogs.RedId}', 'red', 'Red', '{Catalogs.ColorId}')");

        var error = await Assert.ThrowsAsync<LookupSeedException>(() => SeedAsync(db, Catalogs.Colors()));

        Assert.Contains(Catalogs.RedId.ToString(), error.Message, StringComparison.Ordinal);
    }

    private static async Task SeedAsync(LookupTestDb db, LookupCatalog catalog)
    {
        await using var context = db.Context();
        await LookupSeeder.SeedAsync(context, catalog, Cancellation);
    }
}
