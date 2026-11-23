using DotNet.Testcontainers.Configurations;

namespace BuildingBlocks.Testing.Tests;

public sealed class ReaperImageTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task ReaperRunsPinnedImage()
    {
        Assert.False(string.IsNullOrEmpty(postgres.ContainerId));

        var (_, output) = await DockerCli.RunAsync("ps", "--format", "{{.Image}}");
        var reapers = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(image => image.Contains("ryuk", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Assert.True(
            reapers.Count > 0,
            "No reaper container is running. The reaper may be disabled or unable to start in this Docker environment; see research.md R2.");
        Assert.All(reapers, image => Assert.Equal(ImageVersions.Get(TestInfrastructure.ReaperImageKey), image));
    }

    [Fact]
    public void ReaperSettingIsPinnedFromVersionsFile()
    {
        TestInfrastructure.EnsureConfigured();

        Assert.Equal(ImageVersions.Get(TestInfrastructure.ReaperImageKey), TestcontainersSettings.ResourceReaperImage?.FullName);
    }
}
