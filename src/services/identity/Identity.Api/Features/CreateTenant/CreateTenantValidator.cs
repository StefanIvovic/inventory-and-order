using BuildingBlocks.Validation;
using FluentValidation;
using Identity.Api.Tenants;

namespace Identity.Api.Features.CreateTenant;

public sealed class CreateTenantValidator : CommandValidator<CreateTenant>
{
    public CreateTenantValidator()
    {
        RuleFor(x => x.Slug).Must(Tenant.IsValidSlug)
            .WithMessage("The slug must be 3 to 63 lowercase letters, digits or single hyphens, and start and end with a letter or digit.");
        RuleFor(x => x.Name).Must(name => !string.IsNullOrWhiteSpace(name) && name.Trim().Length <= Tenant.NameMaxLength)
            .WithMessage($"The name must be 1 to {Tenant.NameMaxLength} characters.");
        RuleFor(x => x.AdminEmail).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.AdminPassword).NotEmpty().MinimumLength(8);
    }
}
