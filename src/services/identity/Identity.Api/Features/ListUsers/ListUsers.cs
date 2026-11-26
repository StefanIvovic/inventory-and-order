using System.Buffers.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BuildingBlocks.Core.Errors;
using BuildingBlocks.Validation;
using FluentValidation;
using Identity.Api.Authorization;
using Identity.Api.Users;

namespace Identity.Api.Features.ListUsers;

public sealed record ListUsers(
    string? Q = null,
    string? Status = null,
    string? Role = null,
    int Size = ListUsers.DefaultSize,
    string? After = null,
    bool IncludeTotal = false)
{
    public const int DefaultSize = 25;
    public const int MaxSize = 100;
    public const int MaxSearchLength = 256;
}

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

public sealed record UserList(
    IReadOnlyList<UserItem> Items,
    string? NextCursor,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Total,
    UserListActions Actions);

public sealed class ListUsersValidator : CommandValidator<ListUsers>
{
    public ListUsersValidator()
    {
        RuleFor(x => x.Size).InclusiveBetween(1, ListUsers.MaxSize);
        RuleFor(x => x.Q).MaximumLength(ListUsers.MaxSearchLength);
        RuleFor(x => x.Status)
            .Must(status => status is null or StaffStatuses.Active or StaffStatuses.Disabled)
            .WithMessage("The status must be a staff-status code.");
        RuleFor(x => x.Role)
            .Must(role => role is null || IdentityRoles.Tenant.ContainsKey(role))
            .WithMessage("The role must be a tenant role.");
    }
}

internal sealed record UserCursor(string Email, Guid Id)
{
    public const string InvalidCode = "invalid-cursor";

    public string Encode() => Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(this));

    public static UserCursor? Decode(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        try
        {
            if (JsonSerializer.Deserialize<UserCursor>(Base64Url.DecodeFromChars(value)) is { Email: not null } cursor && cursor.Id != Guid.Empty)
            {
                return cursor;
            }
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
        }

        throw new DomainException(InvalidCode, DomainErrorKind.Rule, "The page token is invalid.");
    }
}
