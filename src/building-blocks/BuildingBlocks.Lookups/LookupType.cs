namespace BuildingBlocks.Lookups;

public sealed class LookupType
{
    public const int CodeMaxLength = 100;
    public const int NameMaxLength = 200;
    public const string CodePattern = "^[a-z0-9]+(-[a-z0-9]+)*$";

    private LookupType()
    {
    }

    public Guid Id { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    internal static LookupType From(LookupTypeDefinition definition) => new() { Id = definition.Id, Code = definition.Code, Name = definition.Name };

    internal void Apply(LookupTypeDefinition definition)
    {
        Code = definition.Code;
        Name = definition.Name;
    }
}
