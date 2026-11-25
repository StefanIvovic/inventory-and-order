using System.Text.RegularExpressions;
using Identity.Api.Authorization;

namespace Identity.Tests;

public sealed partial class CatalogueTests
{
    [Fact]
    public void EveryGrantedPermissionIsDefined()
    {
        var granted = IdentityRoles.All.Values.SelectMany(p => p).ToHashSet(StringComparer.Ordinal);

        Assert.Subset(IdentityPermissions.All.ToHashSet(StringComparer.Ordinal), granted);
    }

    [Fact]
    public void NamesAreUniqueLowercaseAndDotSeparated()
    {
        var names = IdentityRoles.All.Keys.Concat(IdentityPermissions.All).ToList();

        Assert.Equal(names.Count, names.Distinct(StringComparer.Ordinal).Count());
        Assert.All(names, name => Assert.Matches(NamePattern(), name));
    }

    [GeneratedRegex("^[a-z]+(-[a-z]+)*(\\.[a-z]+(-[a-z]+)*)+$")]
    private static partial Regex NamePattern();
}
