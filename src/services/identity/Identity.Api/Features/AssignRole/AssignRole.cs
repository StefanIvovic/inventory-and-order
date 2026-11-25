using BuildingBlocks.Validation;
using FluentValidation;
using Identity.Api.Authorization;

namespace Identity.Api.Features.AssignRole;

public sealed record AssignRole(Guid UserId, string Role);

public sealed record UnassignRole(Guid UserId, string Role);

public sealed class AssignRoleValidator : CommandValidator<AssignRole>
{
    public AssignRoleValidator() =>
        RuleFor(x => x.Role).Must(IdentityRoles.Tenant.ContainsKey).WithMessage("The role is not a tenant role.");
}

public sealed class UnassignRoleValidator : CommandValidator<UnassignRole>
{
    public UnassignRoleValidator() =>
        RuleFor(x => x.Role).Must(IdentityRoles.Tenant.ContainsKey).WithMessage("The role is not a tenant role.");
}
