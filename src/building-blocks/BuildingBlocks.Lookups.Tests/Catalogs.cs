namespace BuildingBlocks.Lookups.Tests;

public static class Catalogs
{
    public static readonly Guid ColorId = Guid.Parse("10000000-0000-4000-8000-000000000001");
    public static readonly Guid RedId = Guid.Parse("10000000-0000-4000-8000-000000000002");
    public static readonly Guid GreenId = Guid.Parse("10000000-0000-4000-8000-000000000003");
    public static readonly Guid BlueId = Guid.Parse("10000000-0000-4000-8000-000000000004");
    public static readonly Guid SizeId = Guid.Parse("10000000-0000-4000-8000-000000000005");
    public static readonly Guid SmallId = Guid.Parse("10000000-0000-4000-8000-000000000006");

    public static LookupCatalog Colors() => new(
        new LookupTypeDefinition(
            ColorId,
            "color",
            "Color",
            new LookupValueDefinition(RedId, "red", "Red"),
            new LookupValueDefinition(GreenId, "green", "Green"),
            new LookupValueDefinition(BlueId, "blue", "Blue")),
        new LookupTypeDefinition(SizeId, "size", "Size", new LookupValueDefinition(SmallId, "small", "Small")));

    public static LookupCatalog Many(int count) => new(
        new LookupTypeDefinition(
            ColorId,
            "color",
            "Color",
            [.. Enumerable.Range(1, count).Select(i => new LookupValueDefinition(Guid.Parse($"30000000-0000-4000-8000-{i:D12}"), $"shade-{i:D4}", $"Shade {i:D4}"))]));
}
