using BuildingBlocks.Core.Handlers;
using BuildingBlocks.Messaging;
using Identity.Api.Authorization;
using Identity.Api.Persistence;
using Identity.Api.Users;
using Wolverine;

namespace Identity.Api.Features.DisableUser;

public sealed class DisableUserHandler(IRequestContext context, IdentityDbContext db, HandlerUserManager users)
    : CommandHandler<IdentityDbContext>(context, db)
{
    public async Task<OutgoingMessages> Handle(DisableUser command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        await Permissions.EnsureAsync(IdentityPermissions.UsersManage, cancellationToken);

        var user = await TenantUsers.FindAsync(Db, Tenant, command.UserId, cancellationToken);
        if (user.Id == User.UserId)
        {
            throw UserErrors.CannotDisableSelf();
        }

        if (!user.Disable(Clock.GetUtcNow()))
        {
            return [];
        }

        await users.UpdateSecurityStampAsync(user);
        var tenantId = Tenant.TenantId!;
        return [new Contracts.UserDisabled(user.Id, tenantId), new PermissionAssignmentsChanged(tenantId, user.Id)];
    }
}
