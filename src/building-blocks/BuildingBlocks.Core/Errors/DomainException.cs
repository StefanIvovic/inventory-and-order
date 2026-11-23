namespace BuildingBlocks.Core.Errors;

public class DomainException : Exception
{
    public DomainException(string code, DomainErrorKind kind, string message)
        : base(message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Code = code;
        Kind = kind;
    }

    public string Code { get; }

    public DomainErrorKind Kind { get; }
}
