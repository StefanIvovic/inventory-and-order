namespace BuildingBlocks.Lookups;

public sealed record LookupValueDefinition(Guid Id, string Code, string Name);

public sealed record LookupTypeDefinition(Guid Id, string Code, string Name, IReadOnlyList<LookupValueDefinition> Values)
{
    public LookupTypeDefinition(Guid id, string code, string name, params LookupValueDefinition[] values)
        : this(id, code, name, (IReadOnlyList<LookupValueDefinition>)values)
    {
    }
}
