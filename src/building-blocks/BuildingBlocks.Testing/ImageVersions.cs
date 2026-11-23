namespace BuildingBlocks.Testing;

public static class ImageVersions
{
    public const string PathVariable = "IAO_VERSIONS_FILE";

    private static readonly Lazy<Loaded> Current = new(() =>
    {
        var path = Locate(AppContext.BaseDirectory, Environment.GetEnvironmentVariable(PathVariable));
        return new Loaded(path, Parse(File.ReadAllText(path)));
    });

    public static string Path => Current.Value.FilePath;

    public static string Get(string key) => Get(Current.Value.Values, key, Current.Value.FilePath);

    internal static string Locate(string startDirectory, string? environmentValue)
    {
        if (!string.IsNullOrWhiteSpace(environmentValue))
        {
            return File.Exists(environmentValue)
                ? System.IO.Path.GetFullPath(environmentValue)
                : throw new InvalidOperationException($"{PathVariable} points to '{environmentValue}', which does not exist.");
        }

        for (var directory = new DirectoryInfo(startDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = System.IO.Path.Combine(directory.FullName, "deploy", "versions.env");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException(
            $"Could not find deploy/versions.env above '{startDirectory}'. Set {PathVariable} to its location.");
    }

    internal static IReadOnlyDictionary<string, string> Parse(string text)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var raw in text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            var line = raw.Trim();
            var separator = line.IndexOf('=', StringComparison.Ordinal);
            if (separator > 0)
            {
                values[line[..separator]] = line[(separator + 1)..];
            }
        }

        return values;
    }

    internal static string Get(IReadOnlyDictionary<string, string> values, string key, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidOperationException($"Image key '{key}' is not defined in '{path}'.");
    }

    private sealed record Loaded(string FilePath, IReadOnlyDictionary<string, string> Values);
}
