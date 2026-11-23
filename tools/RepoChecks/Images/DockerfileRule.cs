using System.Text.RegularExpressions;

namespace RepoChecks.Images;

public static partial class DockerfileRule
{
    public static IEnumerable<Finding> Check(string text, string path, VersionsFile versions)
    {
        var findings = new List<Finding>();
        var globalArgs = new HashSet<string>(StringComparer.Ordinal);
        var stages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenFrom = false;
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index].Trim();
            var number = index + 1;

            var arg = ArgPattern().Match(line);
            if (arg.Success && !seenFrom)
            {
                var name = arg.Groups["name"].Value;
                globalArgs.Add(name);
                if (arg.Groups["default"].Success)
                {
                    findings.Add(new Finding(path, number, "IP005", $"ARG {name} must not have a default value"));
                }

                continue;
            }

            var from = FromPattern().Match(line);
            if (!from.Success)
            {
                continue;
            }

            seenFrom = true;
            var image = from.Groups["image"].Value;
            var variable = VariablePattern().Match(image);

            if (variable.Success)
            {
                var key = variable.Groups["key"].Value;
                if (!globalArgs.Contains(key))
                {
                    findings.Add(new Finding(path, number, "IP005", $"FROM uses {key}, which is not declared as ARG before the first FROM"));
                }

                if (!versions.Defines(key))
                {
                    findings.Add(new Finding(path, number, "IP004", $"{key} is not defined in {ImageCheck.VersionsPath}"));
                }
            }
            else if (!stages.Contains(image))
            {
                findings.Add(new Finding(path, number, "IP005", $"FROM '{image}' must come from an ARG supplied by {ImageCheck.VersionsPath}"));
            }

            if (from.Groups["stage"].Success)
            {
                stages.Add(from.Groups["stage"].Value);
            }
        }

        return findings;
    }

    [GeneratedRegex(@"^ARG\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)(?<default>=.*)?$", RegexOptions.IgnoreCase)]
    private static partial Regex ArgPattern();

    [GeneratedRegex(@"^FROM\s+(--\S+\s+)*(?<image>\S+)(\s+AS\s+(?<stage>\S+))?\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex FromPattern();

    [GeneratedRegex(@"^\$\{?(?<key>[A-Z][A-Z0-9_]*)\}?$")]
    private static partial Regex VariablePattern();
}
