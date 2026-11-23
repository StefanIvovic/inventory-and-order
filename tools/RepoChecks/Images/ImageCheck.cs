namespace RepoChecks.Images;

public static class ImageCheck
{
    public const string VersionsPath = "deploy/versions.env";
    public const string GlobalJsonPath = "global.json";

    public static IReadOnlyList<Finding> Run(string root)
    {
        var versionsFile = Path.Combine(root, "deploy", "versions.env");
        if (!File.Exists(versionsFile))
        {
            throw new ConfigurationException($"Missing {VersionsPath} under {root}.");
        }

        var versions = VersionsFile.Parse(File.ReadAllText(versionsFile), VersionsPath);
        var findings = new List<Finding>(versions.Findings);

        var globalJson = Path.Combine(root, "global.json");
        findings.AddRange(SdkRule.Check(versions, File.Exists(globalJson) ? File.ReadAllText(globalJson) : null));

        foreach (var file in SourceWalker.Enumerate(root))
        {
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            var name = Path.GetFileName(file);

            if (SourceWalker.IsCompose(name))
            {
                findings.AddRange(ComposeRule.Check(File.ReadAllText(file), relative, versions));
            }
            else if (SourceWalker.IsDockerfile(name))
            {
                findings.AddRange(DockerfileRule.Check(File.ReadAllText(file), relative, versions));
            }
            else if (SourceWalker.IsTestSource(relative))
            {
                findings.AddRange(TestSourceRule.Check(File.ReadAllText(file), relative, versions));
            }
        }

        return findings
            .OrderBy(f => f.Path, StringComparer.Ordinal)
            .ThenBy(f => f.Line)
            .ThenBy(f => f.RuleId, StringComparer.Ordinal)
            .ToList();
    }
}
