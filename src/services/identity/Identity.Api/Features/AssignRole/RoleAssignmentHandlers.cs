using BuildingBlocks.Core.Handlers;
using BuildingBlocks.Messaging;
using BuildingBlocks.Validation;
using Identity.Api.Authorization;
using Identity.Api.Persistence;
using Identity.Api.Users;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace Identity.Api.Features.AssignRole;

public sealed class AssignRoleHandler(IRequestContext context, IdentityDbContext db, ICommandValidation<AssignRole> validation)
    : CommandHandler<IdentityDbContext>(context, db)
{
    public async Task<OutgoingMessages> Handle(AssignRole command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        await Permissions.EnsureAsync(IdentityPermissions.RolesAssign, cancellationToken);
        await validation.EnsureValidAsync(command, cancellationToken);

        var user = await TenantUsers.FindAsync(Db, Tenant, command.UserId, cancellationToken);
        if (user.IsDisabled)
        {
            throw UserErrors.UserDisabled();
        }

        if (await Db.RoleAssignments.AnyAsync(a => a.UserId == user.Id && a.Role == command.Role, cancellationToken))
        {
            return [];
        }

        Db.RoleAssignments.Add(RoleAssignment.Create(user.Id, command.Role, Clock.GetUtcNow()));
        return [new PermissionAssignmentsChanged(Tenant.TenantId, user.Id)];
    }
}

public sealed class UnassignRoleHandler(IRequestContext context, IdentityDbContext db, ICommandValidation<UnassignRole> validation)
    : CommandHandler<IdentityDbContext>(context, db)
{
    public async Task<OutgoingMessages> Handle(UnassignRole command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        await Permissions.EnsureAsync(IdentityPermissions.RolesAssign, cancellationToken);
        await validation.EnsureValidAsync(command, cancellationToken);

        var user = await TenantUsers.FindAsync(Db, Tenant, command.UserId, cancellationToken);
        if (user.Id == User.UserId && command.Role == IdentityRoles.Administrator)
        {
            throw UserErrors.CannotRemoveOwnAdministrator();
        }

        var assignment = await Db.RoleAssignments.SingleOrDefaultAsync(a => a.UserId == user.Id && a.Role == command.Role, cancellationToken);
        if (assignment is null)
        {
            return [];
        }

        Db.RoleAssignments.Remove(assignment);
        return [new PermissionAssignmentsChanged(Tenant.TenantId, user.Id)];
    }
}
