using BuildingBlocks.Validation;
using Identity.Api.Persistence;
using Identity.Api.Users;
using Identity.Contracts;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace Identity.Api.Features.RegisterCustomer;

public sealed class RegisterCustomerHandler(
    IdentityDbContext db,
    HandlerUserManager users,
    ICommandValidation<RegisterCustomer> validation,
    TimeProvider clock)
{
    public async Task<(RegisteredCustomer, OutgoingMessages)> Handle(RegisterCustomer command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        await validation.EnsureValidAsync(command, cancellationToken);

        if (!await db.Tenants.AnyAsync(t => t.Id == command.TenantId, cancellationToken))
        {
            throw new BuildingBlocks.Core.Errors.NotFoundException("tenant");
        }

        var customer = AppUser.ForTenant(command.Email.Trim(), command.TenantId, UserTypes.Customer, clock.GetUtcNow());
        IdentityResultErrors.ThrowIfFailed(await users.CreateAsync(customer, command.Password), nameof(command.Email), nameof(command.Password));

        return (
            new RegisteredCustomer(customer.Id),
            [new UserRegistered(customer.Id, command.TenantId, customer.UserType, customer.Email!).WithTenantId(command.TenantId)]);
    }
}
