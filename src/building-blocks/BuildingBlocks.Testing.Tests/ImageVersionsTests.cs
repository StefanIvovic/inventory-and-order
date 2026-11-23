namespace BuildingBlocks.Testing.Tests;

public sealed class ImageVersionsTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "iao-versions-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void RepositoryFileIsFoundAndValuesMatchIt()
    {
        var lines = File.ReadAllLines(ImageVersions.Path);
        var postgresLine = lines.Single(l => l.StartsWith("POSTGRES_IMAGE=", StringComparison.Ordinal));

        Assert.EndsWith(Path.Combine("deploy", "versions.env"), ImageVersions.Path, StringComparison.Ordinal);
        Assert.Equal(postgresLine["POSTGRES_IMAGE=".Length..], ImageVersions.Get("POSTGRES_IMAGE"));
    }

    [Fact]
    public void UnknownKeyFailsNamingKeyAndFile()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => ImageVersions.Get("NOPE_IMAGE"));

        Assert.Contains("NOPE_IMAGE", exception.Message, StringComparison.Ordinal);
        Assert.Contains(ImageVersions.Path, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ParseReadsKeyValueLines()
    {
        var text = "A_IMAGE=" + "registry/a" + ":" + "1.2.3" + "\r\nB_IMAGE=" + "b" + ":" + "4.5" + "\n\n";

        var values = ImageVersions.Parse(text);

        Assert.Equal("registry/a" + ":" + "1.2.3", values["A_IMAGE"]);
        Assert.Equal("b" + ":" + "4.5", values["B_IMAGE"]);
    }

    [Fact]
    public void LocateWalksUpToDeployFolder()
    {
        var nested = Directory.CreateDirectory(Path.Combine(root, "src", "x", "bin", "Debug")).FullName;
        Directory.CreateDirectory(Path.Combine(root, "deploy"));
        var file = Path.Combine(root, "deploy", "versions.env");
        File.WriteAllText(file, "A_IMAGE=a" + ":" + "1.0.0\n");

        Assert.Equal(Path.GetFullPath(file), ImageVersions.Locate(nested, null));
    }

    [Fact]
    public void LocatePrefersExplicitPath()
    {
        Directory.CreateDirectory(root);
        var file = Path.Combine(root, "custom.env");
        File.WriteAllText(file, string.Empty);

        Assert.Equal(Path.GetFullPath(file), ImageVersions.Locate(Path.GetTempPath(), file));
    }

    [Fact]
    public void LocateFailsForMissingExplicitPath()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => ImageVersions.Locate(Path.GetTempPath(), Path.Combine(root, "missing.env")));

        Assert.Contains(ImageVersions.PathVariable, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LocateFailsWhenNothingIsFound()
    {
        var lonely = Directory.CreateDirectory(Path.Combine(root, "lonely")).FullName;

        Assert.Throws<InvalidOperationException>(() => ImageVersions.Locate(Path.GetPathRoot(lonely)!, null));
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
