using BuildingBlocks.Core.Handlers;
using BuildingBlocks.Validation;
using Identity.Api.Authorization;
using Identity.Api.Persistence;
using Identity.Api.Users;
using Identity.Contracts;
using Wolverine;

namespace Identity.Api.Features.CreateStaffUser;

public sealed class CreateStaffUserHandler(
    IRequestContext context,
    IdentityDbContext db,
    ICommandValidation<CreateStaffUser> validation,
    HandlerUserManager users) : CommandHandler<IdentityDbContext>(context, db)
{
    public async Task<(CreateStaffUserResponse, OutgoingMessages)> Handle(CreateStaffUser command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        await Permissions.EnsureAsync(IdentityPermissions.UsersManage, cancellationToken);
        await validation.EnsureValidAsync(command, cancellationToken);

        var tenantId = Tenant.TenantId!;
        var user = AppUser.ForTenant(command.Email.Trim(), tenantId, UserTypes.Staff, Clock.GetUtcNow());
        IdentityResultErrors.ThrowIfFailed(await users.CreateAsync(user, command.Password), nameof(command.Email), nameof(command.Password));

        return (new CreateStaffUserResponse(user.Id), [new UserRegistered(user.Id, tenantId, user.UserType, user.Email!)]);
    }
}
