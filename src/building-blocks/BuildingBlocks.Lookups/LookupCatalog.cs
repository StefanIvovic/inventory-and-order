using System.Text.RegularExpressions;

namespace BuildingBlocks.Lookups;

public sealed partial class LookupCatalog
{
    public LookupCatalog(params LookupTypeDefinition[] types)
    {
        ArgumentNullException.ThrowIfNull(types);
        Types = types;
        var problems = Problems(types).ToList();
        if (problems.Count > 0)
        {
            throw new InvalidOperationException("The lookup catalog is invalid: " + string.Join("; ", problems));
        }
    }

    public IReadOnlyList<LookupTypeDefinition> Types { get; }

    internal IReadOnlyList<Guid> Ids => [.. Types.Select(t => t.Id), .. Types.SelectMany(t => t.Values).Select(v => v.Id)];

    private static IEnumerable<string> Problems(IReadOnlyList<LookupTypeDefinition> types)
    {
        var ids = new HashSet<Guid>();
        var typeCodes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var type in types)
        {
            foreach (var problem in Entry("type", type.Id, type.Code, type.Name, ids))
            {
                yield return problem;
            }

            if (!typeCodes.Add(type.Code))
            {
                yield return $"type code '{type.Code}' is used twice";
            }

            var valueCodes = new HashSet<string>(StringComparer.Ordinal);
            foreach (var value in type.Values)
            {
                foreach (var problem in Entry($"value of '{type.Code}'", value.Id, value.Code, value.Name, ids))
                {
                    yield return problem;
                }

                if (!valueCodes.Add(value.Code))
                {
                    yield return $"value code '{value.Code}' is used twice in '{type.Code}'";
                }
            }
        }
    }

    private static IEnumerable<string> Entry(string what, Guid id, string code, string name, HashSet<Guid> ids)
    {
        if (id == Guid.Empty || !ids.Add(id))
        {
            yield return $"{what} '{code}' has an empty or repeated id {id}";
        }

        if (string.IsNullOrEmpty(code) || code.Length > LookupType.CodeMaxLength || !CodeFormat().IsMatch(code))
        {
            yield return $"{what} code '{code}' must be lower-case words joined by '-', at most {LookupType.CodeMaxLength} characters";
        }

        if (string.IsNullOrWhiteSpace(name) || name.Length > LookupType.NameMaxLength)
        {
            yield return $"{what} '{code}' needs a name of 1 to {LookupType.NameMaxLength} characters";
        }
    }

    [GeneratedRegex(LookupType.CodePattern, RegexOptions.CultureInvariant)]
    private static partial Regex CodeFormat();
}
