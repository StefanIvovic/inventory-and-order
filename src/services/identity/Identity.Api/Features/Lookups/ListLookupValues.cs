namespace Identity.Api.Features.Lookups;

public sealed record ListLookupValues(
    string TypeCode,
    string? Q,
    IReadOnlyList<Guid>? Ids,
    IReadOnlyList<string>? Codes,
    int Size,
    string? After);
