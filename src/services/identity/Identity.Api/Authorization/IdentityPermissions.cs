namespace Identity.Api.Authorization;

public static class IdentityPermissions
{
    public const string UsersView = "identity.users.view";
    public const string UsersManage = "identity.users.manage";
    public const string RolesView = "identity.roles.view";
    public const string RolesAssign = "identity.roles.assign";
    public const string TenantsView = "identity.tenants.view";
    public const string TenantsManage = "identity.tenants.manage";

    public static IReadOnlyList<string> All { get; } = [UsersView, UsersManage, RolesView, RolesAssign, TenantsView, TenantsManage];
}
