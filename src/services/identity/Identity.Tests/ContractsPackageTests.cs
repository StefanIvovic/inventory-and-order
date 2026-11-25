using Identity.Contracts;

namespace Identity.Tests;

public sealed class ContractsPackageTests
{
    [Fact]
    public void ContractsReferenceOnlyTheBaseLibrary()
    {
        var references = typeof(TenantCreated).Assembly.GetReferencedAssemblies().Select(a => a.Name!).ToList();

        Assert.All(references, name => Assert.True(name == "netstandard" || name.StartsWith("System", StringComparison.Ordinal), name));
    }

    [Fact]
    public void ContractsHoldExactlyTheThreeEvents()
    {
        var types = typeof(TenantCreated).Assembly.GetExportedTypes().Select(t => t.FullName).Order(StringComparer.Ordinal);

        Assert.Equal(["Identity.Contracts.TenantCreated", "Identity.Contracts.UserDisabled", "Identity.Contracts.UserRegistered"], types);
    }
}
