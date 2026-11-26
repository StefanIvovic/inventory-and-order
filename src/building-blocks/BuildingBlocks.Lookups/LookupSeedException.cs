namespace BuildingBlocks.Lookups;

public sealed class LookupSeedException : Exception
{
    public LookupSeedException()
    {
    }

    public LookupSeedException(string message)
        : base(message)
    {
    }

    public LookupSeedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
