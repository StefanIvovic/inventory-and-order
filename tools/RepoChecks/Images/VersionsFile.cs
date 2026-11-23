using System.Text.RegularExpressions;

namespace RepoChecks.Images;

public sealed record VersionEntry(string Key, string Value, int Line, ImageReference Image);

public sealed partial class VersionsFile
{
    private static readonly HashSet<string> PatchRequired = new(StringComparer.Ordinal)
    {
        "sdk", "aspnet", "runtime", "runtime-deps", "rabbitmq",
    };

    private VersionsFile(string path, IReadOnlyDictionary<string, VersionEntry> entries, IReadOnlyList<Finding> findings)
    {
        Path = path;
        Entries = entries;
        Findings = findings;
    }

    public string Path { get; }

    public IReadOnlyDictionary<string, VersionEntry> Entries { get; }

    public IReadOnlyList<Finding> Findings { get; }

    public IEnumerable<string> RepositoryNames =>
        Entries.Values.SelectMany(e => RepositoryForms(e.Image.Repository)).Distinct(StringComparer.Ordinal);

    public bool Defines(string key) => Entries.ContainsKey(key);

    public static VersionsFile Parse(string text, string path)
    {
        var entries = new Dictionary<string, VersionEntry>(StringComparer.Ordinal);
        var findings = new List<Finding>();
        var repositories = new Dictionary<string, string>(StringComparer.Ordinal);
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            var number = index + 1;
            if (line.Length == 0)
            {
                continue;
            }

            var match = LinePattern().Match(line);
            if (!match.Success)
            {
                findings.Add(new Finding(path, number, "IP001", $"expected KEY_IMAGE=registry/name:tag, found '{line}'"));
                continue;
            }

            var key = match.Groups["key"].Value;
            var value = match.Groups["value"].Value;
            if (entries.ContainsKey(key))
            {
                findings.Add(new Finding(path, number, "IP001", $"duplicate key {key}"));
                continue;
            }

            var image = ImageReference.Parse(value);
            entries[key] = new VersionEntry(key, value, number, image);

            var tagProblem = TagProblem(image);
            if (tagProblem is not null)
            {
                findings.Add(new Finding(path, number, "IP002", $"{key}={value}: {tagProblem}"));
            }

            if (repositories.TryGetValue(image.Repository, out var firstKey))
            {
                findings.Add(new Finding(path, number, "IP008", $"{image.Repository} is defined by both {firstKey} and {key}"));
            }
            else
            {
                repositories[image.Repository] = key;
            }
        }

        return new VersionsFile(path, entries, findings);
    }

    private static string? TagProblem(ImageReference image)
    {
        if (image.Tag is null)
        {
            return "image has no tag";
        }

        if (image.Tag == "latest")
        {
            return "tag 'latest' is forbidden";
        }

        var match = TagPattern().Match(image.Tag);
        if (!match.Success)
        {
            return $"tag '{image.Tag}' is not an exact release version";
        }

        if (!match.Groups["patch"].Success && PatchRequired.Contains(image.ShortName))
        {
            return $"tag '{image.Tag}' floats; {image.ShortName} publishes patch releases";
        }

        return null;
    }

    private static IEnumerable<string> RepositoryForms(string repository)
    {
        yield return repository;
        var firstSlash = repository.IndexOf('/', StringComparison.Ordinal);
        if (firstSlash > 0 && repository[..firstSlash].Contains('.', StringComparison.Ordinal))
        {
            yield return repository[(firstSlash + 1)..];
        }
    }

    [GeneratedRegex(@"^(?<key>[A-Z][A-Z0-9_]*_IMAGE)=(?<value>[^\s#'""=$]+)$")]
    private static partial Regex LinePattern();

    [GeneratedRegex(@"^\d+\.\d+(?<patch>\.\d+)?(-[a-z0-9.]+)*$")]
    private static partial Regex TagPattern();
}
