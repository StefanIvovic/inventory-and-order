namespace Identity.Api.Features.ListUsers;

public sealed record ListUsers;

public sealed record UserActions(bool CanDisable, bool CanAssignRoles);

public sealed record UserItem(
    Guid Id,
    string Email,
    string UserType,
    bool Disabled,
    IReadOnlyList<string> Roles,
    DateTimeOffset CreatedAt,
    UserActions Actions);

public sealed record UserListActions(bool CanCreate);

public sealed record UserList(IReadOnlyList<UserItem> Items, UserListActions Actions);
