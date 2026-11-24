using BuildingBlocks.Validation;
using FluentValidation;

namespace Identity.Api.Features.CreateStaffUser;

public sealed record CreateStaffUser(string Email, string Password);

public sealed record CreateStaffUserResponse(Guid Id);

public sealed class CreateStaffUserValidator : CommandValidator<CreateStaffUser>
{
    public CreateStaffUserValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
    }
}
