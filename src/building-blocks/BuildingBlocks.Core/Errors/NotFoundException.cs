namespace BuildingBlocks.Core.Errors;

public sealed class NotFoundException : Exception
{
    public const string ErrorCode = "not-found";

    public NotFoundException(string resource)
        : this(resource, ErrorCode)
    {
    }

    public NotFoundException(string resource, string code)
        : base(BuildMessage(resource))
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Resource = resource;
        Code = code;
    }

    public string Resource { get; }

    public string Code { get; }

    private static string BuildMessage(string resource)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resource);
        return $"The requested {resource} was not found.";
    }
}
