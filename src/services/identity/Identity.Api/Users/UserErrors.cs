using BuildingBlocks.Core.Errors;

namespace Identity.Api.Users;

public static class UserErrors
{
    public static NotFoundException NotFound() => new("user");

    public static DomainException CannotDisableSelf() =>
        new("cannot-disable-self", DomainErrorKind.Conflict, "You cannot disable your own account.");

    public static DomainException UserDisabled() =>
        new("user-disabled", DomainErrorKind.Conflict, "The user is disabled.");

    public static DomainException CannotRemoveOwnAdministrator() =>
        new("cannot-remove-own-admin", DomainErrorKind.Conflict, "You cannot remove the Administrator role from yourself.");
}
