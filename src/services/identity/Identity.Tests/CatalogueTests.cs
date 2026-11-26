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

    [Fact]
    public void EveryRoleAndPermissionHasADistinctDisplayName()
    {
        Assert.Equal(IdentityRoles.All.Keys.Order(StringComparer.Ordinal), IdentityRoles.DisplayNames.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(IdentityPermissions.All.Order(StringComparer.Ordinal), IdentityPermissions.DisplayNames.Keys.Order(StringComparer.Ordinal));
        Assert.All(IdentityRoles.DisplayNames.Values.Concat(IdentityPermissions.DisplayNames.Values), name => Assert.False(string.IsNullOrWhiteSpace(name)));
        Assert.Equal(IdentityRoles.DisplayNames.Count, IdentityRoles.DisplayNames.Values.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(IdentityPermissions.DisplayNames.Count, IdentityPermissions.DisplayNames.Values.Distinct(StringComparer.Ordinal).Count());
    }

    [GeneratedRegex("^[a-z]+(-[a-z]+)*(\\.[a-z]+(-[a-z]+)*)+$")]
    private static partial Regex NamePattern();
}
