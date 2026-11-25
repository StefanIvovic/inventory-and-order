namespace BuildingBlocks.Lookups;

public sealed class LookupValue
{
    private LookupValue()
    {
    }

    public Guid Id { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public Guid LookupTypeId { get; private set; }

    internal static LookupValue From(Guid typeId, LookupValueDefinition definition) =>
        new() { Id = definition.Id, Code = definition.Code, Name = definition.Name, LookupTypeId = typeId };

    internal void Apply(Guid typeId, LookupValueDefinition definition)
    {
        Code = definition.Code;
        Name = definition.Name;
        LookupTypeId = typeId;
    }
}
