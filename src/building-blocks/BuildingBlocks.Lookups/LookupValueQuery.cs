namespace BuildingBlocks.Lookups;

public sealed record LookupValueQuery(
    string TypeCode,
    string? Q = null,
    IReadOnlyList<Guid>? Ids = null,
    IReadOnlyList<string>? Codes = null,
    int Size = LookupValueQuery.DefaultSize,
    string? After = null)
{
    public const int DefaultSize = 50;
    public const int MaxSize = 200;
    public const int MaxListed = 200;
    public const int MaxSearchLength = 100;
}

public sealed record LookupValueItem(Guid Id, string Code, string Name);

public sealed record LookupValuePage(IReadOnlyList<LookupValueItem> Items, string? NextCursor);
