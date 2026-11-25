using BuildingBlocks.Validation;
using FluentValidation;

namespace Identity.Api.Features.RegisterCustomer;

public sealed record RegisterCustomer(string TenantId, string Email, string Password, string ConfirmPassword);

public sealed record RegisteredCustomer(Guid UserId);

public sealed class RegisterCustomerValidator : CommandValidator<RegisterCustomer>
{
    public RegisterCustomerValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
        RuleFor(x => x.ConfirmPassword).Equal(x => x.Password).WithMessage("The passwords do not match.");
    }
}
