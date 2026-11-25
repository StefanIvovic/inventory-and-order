using BuildingBlocks.Core.Errors;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Identity;

namespace Identity.Api.Users;

public static class IdentityResultErrors
{
    public const string EmailTaken = "email-taken";

    public static void ThrowIfFailed(IdentityResult result, string emailField, string passwordField)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.Succeeded)
        {
            return;
        }

        if (result.Errors.Any(e => e.Code is nameof(IdentityErrorDescriber.DuplicateEmail) or nameof(IdentityErrorDescriber.DuplicateUserName)))
        {
            throw EmailTakenError();
        }

        throw new ValidationException(result.Errors.Select(e => new ValidationFailure(
            e.Code.StartsWith("Password", StringComparison.Ordinal) ? passwordField : emailField,
            e.Description)));
    }

    public static DomainException EmailTakenError() =>
        new(EmailTaken, DomainErrorKind.Conflict, "The email address is already in use.");
}
