namespace BuildingBlocks.Core.Permissions;

public sealed class PermissionDeniedException : Exception
{
    public const string ErrorCode = "permission-denied";

    public PermissionDeniedException(IEnumerable<string> missing)
        : this(Normalize(missing))
    {
    }

    private PermissionDeniedException(IReadOnlyList<string> missing)
        : base($"Missing permissions: {string.Join(", ", missing)}")
    {
        Missing = missing;
    }

    public IReadOnlyList<string> Missing { get; }

    public string Code { get; } = ErrorCode;

    private static List<string> Normalize(IEnumerable<string> missing)
    {
        ArgumentNullException.ThrowIfNull(missing);
        var distinct = missing.Distinct(StringComparer.Ordinal).ToList();
        if (distinct.Count == 0)
        {
            throw new ArgumentException("At least one missing permission is required.", nameof(missing));
        }

        return distinct;
    }
}
