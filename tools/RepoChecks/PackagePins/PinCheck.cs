using System.Xml;
using System.Xml.Linq;

namespace RepoChecks.PackagePins;

public static class PinCheck
{
    public const string ServicesFolder = "src/services";

    public static IReadOnlyList<Finding> Run(string root)
    {
        var services = Path.Combine(root, "src", "services");
        if (!Directory.Exists(services))
        {
            return [];
        }

        var findings = new List<Finding>();
        foreach (var props in Directory.EnumerateDirectories(services)
                     .Select(folder => Path.Combine(folder, "Directory.Packages.props"))
                     .Where(File.Exists)
                     .Order(StringComparer.Ordinal))
        {
            var relative = Path.GetRelativePath(root, props).Replace('\\', '/');
            findings.AddRange(Check(File.ReadAllText(props), relative));
        }

        return findings;
    }

    public static IEnumerable<Finding> Check(string text, string path)
    {
        var document = XDocument.Parse(text, LoadOptions.SetLineInfo);
        foreach (var element in document.Descendants("PackageVersion"))
        {
            var id = (string?)element.Attribute("Include") ?? string.Empty;
            var version = (string?)element.Attribute("Version") ?? string.Empty;
            if (!IsShared(id))
            {
                continue;
            }

            var line = ((IXmlLineInfo)element).LineNumber;
            if (version.Length == 0 || version.Contains('-', StringComparison.Ordinal) || version.IndexOfAny(['*', '[', '(', ',']) >= 0)
            {
                yield return new Finding(path, line, "PV001", $"{id} must be pinned to an exact tagged release, not '{version}'");
            }
        }
    }

    private static bool IsShared(string id) =>
        id.StartsWith("BuildingBlocks.", StringComparison.Ordinal) ||
        id.EndsWith(".Contracts", StringComparison.Ordinal);
}
