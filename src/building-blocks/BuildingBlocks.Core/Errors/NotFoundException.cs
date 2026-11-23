namespace BuildingBlocks.Core.Errors;

public sealed class NotFoundException : Exception
{
    public const string ErrorCode = "not-found";

    public NotFoundException(string resource)
        : base(BuildMessage(resource))
    {
        Resource = resource;
    }

    public string Resource { get; }

    public string Code { get; } = ErrorCode;

    private static string BuildMessage(string resource)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resource);
        return $"The requested {resource} was not found.";
    }
}
