using Microsoft.Extensions.Logging;

namespace BuildingBlocks.AspNetCore.Errors;

public sealed record MappedProblem(
    int Status,
    string? Code,
    string? Detail,
    IReadOnlyDictionary<string, object?> Extensions,
    LogLevel LogLevel,
    string? Title = null);
