namespace Identity.Api.Authorization;

public static class IdentityPermissions
{
    public const string RolesView = "identity.roles.view";
    public const string RolesAssign = "identity.roles.assign";

    public static IReadOnlyList<string> All { get; } = [RolesView, RolesAssign];
}
