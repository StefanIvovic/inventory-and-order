using System.Text.RegularExpressions;
using YamlDotNet.RepresentationModel;

namespace RepoChecks.Images;

public static partial class ComposeRule
{
    public static IEnumerable<Finding> Check(string text, string path, VersionsFile versions)
    {
        var stream = new YamlStream();
        using (var reader = new StringReader(text))
        {
            stream.Load(reader);
        }

        var findings = new List<Finding>();
        foreach (var document in stream.Documents)
        {
            if (document.RootNode is not YamlMappingNode root ||
                !root.Children.TryGetValue(new YamlScalarNode("services"), out var servicesNode) ||
                servicesNode is not YamlMappingNode services)
            {
                continue;
            }

            foreach (var service in services.Children.Values.OfType<YamlMappingNode>())
            {
                if (!service.Children.TryGetValue(new YamlScalarNode("image"), out var imageNode) ||
                    imageNode is not YamlScalarNode image)
                {
                    continue;
                }

                var value = image.Value ?? string.Empty;
                var line = (int)image.Start.Line;
                var match = VariablePattern().Match(value);
                if (!match.Success)
                {
                    findings.Add(new Finding(path, line, "IP003", $"image '{value}' must be exactly ${{KEY}} from {ImageCheck.VersionsPath}"));
                }
                else if (!versions.Defines(match.Groups["key"].Value))
                {
                    findings.Add(new Finding(path, line, "IP004", $"{match.Groups["key"].Value} is not defined in {ImageCheck.VersionsPath}"));
                }
            }
        }

        return findings;
    }

    [GeneratedRegex(@"^\$\{(?<key>[A-Z][A-Z0-9_]*)\}$")]
    private static partial Regex VariablePattern();
}
