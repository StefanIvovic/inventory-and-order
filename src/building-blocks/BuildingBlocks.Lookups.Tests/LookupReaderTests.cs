using BuildingBlocks.Core.Errors;
using BuildingBlocks.Testing;
using FluentValidation;

namespace BuildingBlocks.Lookups.Tests;

public sealed class LookupReaderTests(PostgresFixture postgres)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ValuesAreSortedByName()
    {
        var page = await ReadAsync(await SeededAsync(Catalogs.Colors()), new LookupValueQuery("color"));

        Assert.Equal(["Blue", "Green", "Red"], page.Items.Select(i => i.Name));
        Assert.Equal(Catalogs.BlueId, page.Items[0].Id);
        Assert.Equal("blue", page.Items[0].Code);
        Assert.Null(page.NextCursor);
    }

    [Theory]
    [InlineData("RE", new[] { "Green", "Red" })]
    [InlineData("blu", new[] { "Blue" })]
    [InlineData("  ee ", new[] { "Green" })]
    [InlineData("%", new string[0])]
    [InlineData("_", new string[0])]
    public async Task SearchMatchesNameIgnoringCaseOrCode(string q, string[] expected)
    {
        var page = await ReadAsync(await SeededAsync(Catalogs.Colors()), new LookupValueQuery("color", Q: q));

        Assert.Equal(expected, page.Items.Select(i => i.Name));
    }

    [Fact]
    public async Task SearchMatchesTheCode()
    {
        var db = await SeededAsync(new LookupCatalog(new LookupTypeDefinition(
            Catalogs.ColorId, "color", "Color", new LookupValueDefinition(Catalogs.RedId, "rgb-ff0000", "Red"))));

        var page = await ReadAsync(db, new LookupValueQuery("color", Q: "ff00"));

        Assert.Equal(["Red"], page.Items.Select(i => i.Name));
    }

    [Fact]
    public async Task IdsAndCodesSelectThoseValues()
    {
        var db = await SeededAsync(Catalogs.Colors());

        var byIds = await ReadAsync(db, new LookupValueQuery("color", Ids: [Catalogs.RedId, Catalogs.SmallId, Guid.NewGuid()]));
        var byCodes = await ReadAsync(db, new LookupValueQuery("color", Codes: ["green", "small", "nope"]));
        var combined = await ReadAsync(db, new LookupValueQuery("color", Q: "e", Codes: ["blue", "red"]));

        Assert.Equal(["Red"], byIds.Items.Select(i => i.Name));
        Assert.Equal(["Green"], byCodes.Items.Select(i => i.Name));
        Assert.Equal(["Blue", "Red"], combined.Items.Select(i => i.Name));
    }

    [Fact]
    public async Task PagesWalkEveryValueOnceWithOneCommandEach()
    {
        var db = await SeededAsync(Catalogs.Many(23));
        var seen = new List<string>();
        string? after = null;
        var pages = 0;

        do
        {
            db.Commands.Reset();
            var page = await ReadAsync(db, new LookupValueQuery("color", Size: 5, After: after));
            Assert.Equal(1, db.Commands.Count);
            seen.AddRange(page.Items.Select(i => i.Code));
            after = page.NextCursor;
            pages++;
        }
        while (after is not null);

        Assert.Equal(5, pages);
        Assert.Equal(Enumerable.Range(1, 23).Select(i => $"shade-{i:D4}"), seen);
    }

    [Fact]
    public async Task ExactlyFullLastPageHasNoCursor()
    {
        var page = await ReadAsync(await SeededAsync(Catalogs.Colors()), new LookupValueQuery("color", Size: 3));

        Assert.Equal(3, page.Items.Count);
        Assert.Null(page.NextCursor);
    }

    [Fact]
    public async Task NothingMatchingIsAnEmptyPage()
    {
        var page = await ReadAsync(await SeededAsync(Catalogs.Colors()), new LookupValueQuery("color", Q: "zzz"));

        Assert.Empty(page.Items);
        Assert.Null(page.NextCursor);
    }

    [Fact]
    public async Task UnknownTypeIsNotFound()
    {
        var db = await SeededAsync(Catalogs.Colors());

        var error = await Assert.ThrowsAsync<NotFoundException>(() => ReadAsync(db, new LookupValueQuery("shape")));

        Assert.Equal(LookupReader.TypeNotFoundCode, error.Code);
    }

    [Theory]
    [InlineData(0, null, 0, 0, "Size")]
    [InlineData(201, null, 0, 0, "Size")]
    [InlineData(50, 101, 0, 0, "Q")]
    [InlineData(50, null, 201, 0, "Ids")]
    [InlineData(50, null, 0, 201, "Codes")]
    public async Task InvalidFiltersAreRefused(int size, int? searchLength, int idCount, int codeCount, string field)
    {
        var db = await SeededAsync(Catalogs.Colors());
        var query = new LookupValueQuery(
            "color",
            Q: searchLength is { } length ? new string('a', length) : null,
            Ids: [.. Enumerable.Range(0, idCount).Select(_ => Guid.NewGuid())],
            Codes: [.. Enumerable.Range(0, codeCount).Select(i => $"c-{i}")],
            Size: size);

        var error = await Assert.ThrowsAsync<ValidationException>(() => ReadAsync(db, query));

        Assert.Equal([field], error.Errors.Select(e => e.PropertyName).Distinct());
    }

    [Theory]
    [InlineData("not-base64!")]
    [InlineData("e30")]
    [InlineData("bm90IGpzb24")]
    public async Task MalformedPageTokenIsRefused(string after)
    {
        var db = await SeededAsync(Catalogs.Colors());

        var error = await Assert.ThrowsAsync<ValidationException>(() => ReadAsync(db, new LookupValueQuery("color", After: after)));

        Assert.Equal(["After"], error.Errors.Select(e => e.PropertyName).Distinct());
    }

    private async Task<LookupTestDb> SeededAsync(LookupCatalog catalog)
    {
        var db = await LookupTestDb.CreateAsync(postgres);
        await using var context = db.Context();
        await LookupSeeder.SeedAsync(context, catalog, Cancellation);
        return db;
    }

    private static async Task<LookupValuePage> ReadAsync(LookupTestDb db, LookupValueQuery query)
    {
        await using var context = db.Context();
        return await context.ReadValuesAsync(query, Cancellation);
    }
}
