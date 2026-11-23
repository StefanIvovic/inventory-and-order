using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace RepoChecks.Images;

public static class TestSourceRule
{
    private static readonly HashSet<SyntaxKind> StringTokens =
    [
        SyntaxKind.StringLiteralToken,
        SyntaxKind.Utf8StringLiteralToken,
        SyntaxKind.InterpolatedStringTextToken,
        SyntaxKind.SingleLineRawStringLiteralToken,
        SyntaxKind.MultiLineRawStringLiteralToken,
        SyntaxKind.Utf8SingleLineRawStringLiteralToken,
        SyntaxKind.Utf8MultiLineRawStringLiteralToken,
    ];

    public static IEnumerable<Finding> Check(string text, string path, VersionsFile versions)
    {
        var markers = versions.RepositoryNames.Select(name => name + ":").ToList();
        if (markers.Count == 0)
        {
            return [];
        }

        var root = CSharpSyntaxTree.ParseText(text).GetRoot();
        var findings = new List<Finding>();

        foreach (var token in root.DescendantTokens().Where(t => StringTokens.Contains(t.Kind())))
        {
            var value = token.ValueText;
            var marker = markers.FirstOrDefault(m => value.Contains(m, StringComparison.Ordinal));
            if (marker is not null)
            {
                var line = token.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                findings.Add(new Finding(path, line, "IP006", $"image literal '{marker}...' in test source; read the image from {ImageCheck.VersionsPath}"));
            }
        }

        return findings;
    }
}
