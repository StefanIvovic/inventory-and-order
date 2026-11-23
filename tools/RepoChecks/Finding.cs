namespace RepoChecks;

public sealed record Finding(string Path, int Line, string RuleId, string Detail)
{
    public override string ToString() => $"{Path}:{Line}: {RuleId} {Detail}";
}
