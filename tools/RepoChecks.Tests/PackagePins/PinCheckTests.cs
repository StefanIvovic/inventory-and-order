using RepoChecks.PackagePins;

namespace RepoChecks.Tests.PackagePins;

public sealed class PinCheckTests
{
    [Theory]
    [InlineData("BuildingBlocks.Core", "0.1.0")]
    [InlineData("Order.Contracts", "1.2.3")]
    [InlineData("WolverineFx", "6.65.1-beta.1")]
    public void ExactOrUnrelatedPinsPass(string id, string version)
    {
        using var repo = new TempRepo().Write("src/services/identity/Directory.Packages.props", Props(id, version));

        Assert.Empty(PinCheck.Run(repo.Root));
    }

    [Theory]
    [InlineData("BuildingBlocks.Core", "0.0.0-alpha.0.10")]
    [InlineData("BuildingBlocks.Testing", "0.*")]
    [InlineData("Identity.Contracts", "[1.0.0,2.0.0)")]
    [InlineData("BuildingBlocks.Messaging", "")]
    public void PrereleaseOrFloatingSharedPinIsPV001(string id, string version)
    {
        using var repo = new TempRepo().Write("src/services/identity/Directory.Packages.props", Props(id, version));

        var finding = Assert.Single(PinCheck.Run(repo.Root));

        Assert.Equal("src/services/identity/Directory.Packages.props", finding.Path);
        Assert.Equal(3, finding.Line);
        Assert.Equal("PV001", finding.RuleId);
    }

    [Fact]
    public void RepositoryWithoutServicesHasNoFindings()
    {
        using var repo = new TempRepo();

        Assert.Empty(PinCheck.Run(repo.Root));
    }

    [Fact]
    public void CommandLineRunsPackagesCheck()
    {
        using var repo = new TempRepo().Write("src/services/identity/Directory.Packages.props", Props("BuildingBlocks.Core", "0.0.0-alpha.0.1"));
        using var output = new StringWriter();
        using var error = new StringWriter();

        var code = CommandLine.Run(["packages", "--root", repo.Root], output, error);

        Assert.Equal(CommandLine.FindingsReported, code);
        Assert.Contains("PV001", output.ToString(), StringComparison.Ordinal);
    }

    private static string Props(string id, string version) => string.Join('\n',
        "<Project>",
        "  <ItemGroup>",
        $"    <PackageVersion Include=\"{id}\" Version=\"{version}\" />",
        "  </ItemGroup>",
        "</Project>",
        "");
}
