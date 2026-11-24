namespace Identity.Api.Features.ListRoles;

public sealed record GetRoles;

public sealed record RoleDto(string Name, IReadOnlyList<string> Permissions);

public sealed record RolesResponse(IReadOnlyList<RoleDto> Roles, bool CanAssign);
