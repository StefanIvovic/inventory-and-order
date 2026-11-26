namespace Identity.Api.Features.ListRoles;

public sealed record GetRoles;

public sealed record PermissionDto(string Name, string DisplayName);

public sealed record RoleDto(string Name, string DisplayName, IReadOnlyList<PermissionDto> Permissions);

public sealed record RolesResponse(IReadOnlyList<RoleDto> Roles, bool CanAssign);
