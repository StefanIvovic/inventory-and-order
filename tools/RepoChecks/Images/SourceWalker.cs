using System.Text.RegularExpressions;

namespace RepoChecks.Images;

public static partial class SourceWalker
{
    private static readonly HashSet<string> ExcludedFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin", "obj", "node_modules", ".git", ".specify", ".claude", "artifacts", "specs",
    };

    public static IEnumerable<string> Enumerate(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            var directory = pending.Pop();
            foreach (var file in Directory.EnumerateFiles(directory))
            {
                yield return file;
            }

            foreach (var child in Directory.EnumerateDirectories(directory))
            {
                if (!ExcludedFolders.Contains(Path.GetFileName(child)))
                {
                    pending.Push(child);
                }
            }
        }
    }

    public static bool IsCompose(string fileName) => ComposePattern().IsMatch(fileName);

    public static bool IsDockerfile(string fileName) =>
        fileName.StartsWith("Dockerfile", StringComparison.Ordinal) ||
        fileName.EndsWith(".Dockerfile", StringComparison.Ordinal);

    public static bool IsTestSource(string relativePath) =>
        relativePath.EndsWith(".cs", StringComparison.Ordinal) &&
        relativePath.Split('/').SkipLast(1).Any(segment => segment.EndsWith(".Tests", StringComparison.Ordinal));

    [GeneratedRegex(@"^(docker-)?compose([.-][\w.-]+)?\.ya?ml$")]
    private static partial Regex ComposePattern();
}
