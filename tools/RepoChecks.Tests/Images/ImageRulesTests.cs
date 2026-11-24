using RepoChecks.Images;
using static RepoChecks.Tests.TempRepo;

namespace RepoChecks.Tests.Images;

public sealed class ImageRulesTests
{
    private static readonly string ValidCompose = string.Join('\n',
        "services:",
        "  db:",
        "    image: ${POSTGRES_IMAGE}",
        "  broker:",
        "    image: ${RABBITMQ_IMAGE}",
        "");

    private static readonly string ValidDockerfile = string.Join('\n',
        "ARG DOTNET_SDK_IMAGE",
        "ARG DOTNET_ASPNET_IMAGE",
        "FROM ${DOTNET_SDK_IMAGE} AS build",
        "WORKDIR /src",
        "FROM ${DOTNET_ASPNET_IMAGE} AS final",
        "COPY --from=build /src /app",
        "");

    [Fact]
    public void ValidRepositoryHasNoFindings()
    {
        using var repo = WithValidBaseline()
            .Write("src/services/order/compose.yaml", ValidCompose)
            .Write("src/services/order/Dockerfile", ValidDockerfile)
            .Write("src/services/order/Order.Tests/Fixture.cs", "public static class Fixture { public const string Key = \"POSTGRES_IMAGE\"; }");

        Assert.Empty(repo.Run());
    }

    [Theory]
    [InlineData("POSTGRES_IMAGE postgres")]
    [InlineData("postgres_image=x")]
    [InlineData("POSTGRES=pg")]
    [InlineData("# pinned")]
    public void MalformedVersionsLineIsIP001(string line)
    {
        using var repo = WithValidBaseline();
        AppendVersions(repo, line);

        AssertFinding(repo, "IP001", "deploy/versions.env");
    }

    [Theory]
    [InlineData("postgres", "latest")]
    [InlineData("postgres", "18")]
    [InlineData("postgres", "18-alpine3.24")]
    [InlineData("rabbitmq", "4.4")]
    [InlineData("rabbitmq", "4.4-management-alpine")]
    [InlineData("mcr.microsoft.com/dotnet/aspnet", "10.0")]
    [InlineData("mcr.microsoft.com/dotnet/aspnet", "10.0-alpine3.24")]
    public void FloatingOrLatestTagIsIP002(string repository, string tag)
    {
        using var repo = WithValidBaseline();
        AppendVersions(repo, $"EXTRA_IMAGE={Image(repository, tag)}");

        AssertFinding(repo, "IP002", "deploy/versions.env");
    }

    [Fact]
    public void UntaggedImageIsIP002()
    {
        using var repo = WithValidBaseline();
        AppendVersions(repo, "EXTRA_IMAGE=redis");

        AssertFinding(repo, "IP002", "deploy/versions.env");
    }

    [Fact]
    public void PostgresMajorMinorIsAccepted()
    {
        using var repo = WithValidBaseline();

        Assert.DoesNotContain(repo.Run(), f => f.RuleId == "IP002");
    }

    [Theory]
    [InlineData("    image: postgres")]
    [InlineData("    image: ${POSTGRES_IMAGE}-x")]
    [InlineData("    image: $POSTGRES_IMAGE")]
    public void ComposeImageNotExactlyVariableIsIP003(string imageLine)
    {
        using var repo = WithValidBaseline()
            .Write("deploy/compose.yaml", string.Join('\n', "services:", "  db:", imageLine, ""));

        AssertFinding(repo, "IP003", "deploy/compose.yaml");
    }

    [Fact]
    public void ComposeLiteralTagIsIP003()
    {
        using var repo = WithValidBaseline()
            .Write("deploy/compose.yaml", string.Join('\n', "services:", "  db:", "    image: " + Image("postgres", "18.7-alpine3.24"), ""));

        AssertFinding(repo, "IP003", "deploy/compose.yaml");
    }

    [Fact]
    public void ComposeUndefinedVariableIsIP004()
    {
        using var repo = WithValidBaseline()
            .Write("deploy/compose.yaml", string.Join('\n', "services:", "  cache:", "    image: ${REDIS_IMAGE}", ""));

        AssertFinding(repo, "IP004", "deploy/compose.yaml");
    }

    [Fact]
    public void DockerfileUndefinedVariableIsIP004()
    {
        using var repo = WithValidBaseline()
            .Write("src/gateway/Dockerfile", string.Join('\n', "ARG NODE_IMAGE", "FROM ${NODE_IMAGE}", ""));

        AssertFinding(repo, "IP004", "src/gateway/Dockerfile");
    }

    [Fact]
    public void DockerfileLiteralBaseImageIsIP005()
    {
        using var repo = WithValidBaseline()
            .Write("src/gateway/Dockerfile", string.Join('\n', "FROM " + Image("mcr.microsoft.com/dotnet/sdk", "10.0.401-alpine3.24") + " AS build", ""));

        AssertFinding(repo, "IP005", "src/gateway/Dockerfile");
    }

    [Fact]
    public void DockerfileArgWithDefaultIsIP005()
    {
        using var repo = WithValidBaseline()
            .Write("src/gateway/Dockerfile", string.Join('\n', "ARG DOTNET_SDK_IMAGE=" + Image("mcr.microsoft.com/dotnet/sdk", "10.0.401-alpine3.24"), "FROM ${DOTNET_SDK_IMAGE}", ""));

        AssertFinding(repo, "IP005", "src/gateway/Dockerfile");
    }

    [Fact]
    public void DockerfileUndeclaredArgIsIP005()
    {
        using var repo = WithValidBaseline()
            .Write("src/gateway/Dockerfile", string.Join('\n', "FROM ${DOTNET_SDK_IMAGE}", ""));

        AssertFinding(repo, "IP005", "src/gateway/Dockerfile");
    }

    [Fact]
    public void DockerfileStageReferenceIsAccepted()
    {
        using var repo = WithValidBaseline()
            .Write("src/gateway/Dockerfile", string.Join('\n', "ARG DOTNET_SDK_IMAGE", "FROM ${DOTNET_SDK_IMAGE} AS build", "FROM build AS publish", ""));

        Assert.Empty(repo.Run());
    }

    [Fact]
    public void TestSourceImageLiteralIsIP006()
    {
        var literal = "\"" + Image("postgres", "18.7-alpine3.24") + "\"";
        using var repo = WithValidBaseline()
            .Write("src/services/order/Order.Tests/DbFixture.cs", "public static class DbFixture { public const string Image = " + literal + "; }");

        AssertFinding(repo, "IP006", "src/services/order/Order.Tests/DbFixture.cs");
    }

    [Fact]
    public void ImageLiteralOutsideTestProjectsIsNotIP006()
    {
        var literal = "\"" + Image("postgres", "18.7-alpine3.24") + "\"";
        using var repo = WithValidBaseline()
            .Write("src/services/order/Order.Api/Settings.cs", "public static class Settings { public const string Image = " + literal + "; }");

        Assert.Empty(repo.Run());
    }

    [Fact]
    public void SdkImageNotMatchingGlobalJsonIsIP007()
    {
        using var repo = new TempRepo()
            .Write("global.json", """{ "sdk": { "version": "10.0.401", "rollForward": "disable" } }""")
            .Write("deploy/versions.env", $"DOTNET_SDK_IMAGE={Image("mcr.microsoft.com/dotnet/sdk", "10.0.404-alpine3.24")}\n");

        AssertFinding(repo, "IP007", "deploy/versions.env");
    }

    [Fact]
    public void SameRepositoryUnderTwoKeysIsIP008()
    {
        using var repo = WithValidBaseline();
        AppendVersions(repo, $"POSTGRES_TEST_IMAGE={Image("postgres", "18.6-alpine3.24")}");

        AssertFinding(repo, "IP008", "deploy/versions.env");
    }

    [Fact]
    public void ExcludedFoldersAreNotScanned()
    {
        using var repo = WithValidBaseline()
            .Write("node_modules/pkg/compose.yaml", string.Join('\n', "services:", "  x:", "    image: postgres", ""))
            .Write("artifacts/Dockerfile", "FROM postgres\n");

        Assert.Empty(repo.Run());
    }

    [Fact]
    public void FindingsReportRelativePathAndLine()
    {
        using var repo = WithValidBaseline()
            .Write("deploy/compose.yaml", string.Join('\n', "services:", "  db:", "    image: postgres", ""));

        var finding = Assert.Single(repo.Run());

        Assert.Equal("deploy/compose.yaml:3: IP003", finding.ToString()[..28]);
    }

    [Fact]
    public void RealDeployComposeFilesHaveNoFindings()
    {
        var root = FindRepositoryRoot();
        var versions = VersionsFile.Parse(File.ReadAllText(Path.Combine(root, "deploy", "versions.env")), "deploy/versions.env");

        foreach (var relative in new[] { "deploy/compose.yaml", "deploy/infra/compose.yaml" })
        {
            var text = File.ReadAllText(Path.Combine(root, relative));
            Assert.Empty(ComposeRule.Check(text, relative, versions));
        }

        Assert.Empty(versions.Findings);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "deploy", "versions.env")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("deploy/versions.env not found above the test output folder.");
    }

    private static void AppendVersions(TempRepo repo, string line)
    {
        var path = Path.Combine(repo.Root, "deploy", "versions.env");
        File.AppendAllText(path, line + "\n");
    }

    private static void AssertFinding(TempRepo repo, string ruleId, string path)
    {
        var findings = repo.Run();
        Assert.Contains(findings, f => f.RuleId == ruleId && f.Path == path);
    }
}
