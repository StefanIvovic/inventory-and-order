using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Images;

namespace BuildingBlocks.Testing;

public static class TestInfrastructure
{
    public const string ReaperImageKey = "RYUK_IMAGE";

    private static readonly Lazy<bool> Configured = new(() =>
    {
        TestcontainersSettings.ResourceReaperImage = new DockerImage(ImageVersions.Get(ReaperImageKey));
        return true;
    });

    public static void EnsureConfigured() => _ = Configured.Value;
}
