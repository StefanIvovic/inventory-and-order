using Identity.Api.Authorization;

namespace Identity.Tests;

public sealed class RoleTableTests
{
    [Fact]
    public void TenantRolesNeverGrantPlatformPermissions()
    {
        var tenant = IdentityRoles.Tenant.Values.SelectMany(p => p).ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain(IdentityPermissions.TenantsView, tenant);
        Assert.DoesNotContain(IdentityPermissions.TenantsManage, tenant);
    }

    [Fact]
    public void PlatformRolesGrantOnlyTenantPermissions()
    {
        var platform = IdentityRoles.Platform.Values.SelectMany(p => p).ToHashSet(StringComparer.Ordinal);

        Assert.Equal([IdentityPermissions.TenantsManage, IdentityPermissions.TenantsView], platform.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void ViewerIsASubsetOfAdministrator()
    {
        Assert.Subset(
            IdentityRoles.Tenant[IdentityRoles.Administrator].ToHashSet(StringComparer.Ordinal),
            IdentityRoles.Tenant[IdentityRoles.Viewer].ToHashSet(StringComparer.Ordinal));
        Assert.DoesNotContain(IdentityPermissions.UsersManage, IdentityRoles.Tenant[IdentityRoles.Viewer]);
    }

    [Fact]
    public void TenantAndPlatformRoleNamesDoNotOverlap()
    {
        Assert.Empty(IdentityRoles.Tenant.Keys.Intersect(IdentityRoles.Platform.Keys, StringComparer.Ordinal));
    }
}
