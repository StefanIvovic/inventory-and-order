using System.Reflection;

namespace Product.Tests;

public sealed class ContractsPackageTests
{
    private static readonly Assembly Contracts = Assembly.Load("Product.Contracts");

    [Fact]
    public void ContractsReferenceOnlyTheBaseLibrary()
    {
        var references = Contracts.GetReferencedAssemblies().Select(a => a.Name!).ToList();

        Assert.All(references, name => Assert.True(name == "netstandard" || name.StartsWith("System", StringComparison.Ordinal), name));
    }

    [Fact]
    public void ContractsHoldNoMessagesYet()
    {
        Assert.Empty(Contracts.GetExportedTypes());
    }
}
