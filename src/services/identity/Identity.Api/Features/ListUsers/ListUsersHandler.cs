using BuildingBlocks.Core.Handlers;
using Identity.Api.Authorization;
using Identity.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Identity.Api.Features.ListUsers;

public sealed class ListUsersHandler(IRequestContext context, IdentityDbContext db) : CommandHandler<IdentityDbContext>(context, db)
{
    public async Task<UserList> Handle(ListUsers query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        await Permissions.EnsureAsync(IdentityPermissions.UsersView, cancellationToken);

        var tenantId = Tenant.TenantId;
        var users = await Db.Users
            .Where(u => u.TenantId == tenantId)
            .OrderBy(u => u.NormalizedEmail)
            .Select(u => new { u.Id, u.Email, u.UserType, u.DisabledAt, u.CreatedAt })
            .ToListAsync(cancellationToken);
        var roles = (await Db.RoleAssignments.Select(a => new { a.UserId, a.Role }).ToListAsync(cancellationToken))
            .ToLookup(a => a.UserId, a => a.Role);
        var canManage = await Permissions.HasAsync(IdentityPermissions.UsersManage, cancellationToken);
        var canAssign = await Permissions.HasAsync(IdentityPermissions.RolesAssign, cancellationToken);

        var items = users.Select(u => new UserItem(
                u.Id,
                u.Email!,
                u.UserType,
                u.DisabledAt is not null,
                roles[u.Id].Order(StringComparer.Ordinal).ToList(),
                u.CreatedAt,
                new UserActions(
                    canManage && u.DisabledAt is null && u.Id != User.UserId,
                    canAssign && u.DisabledAt is null)))
            .ToList();

        return new UserList(items, new UserListActions(canManage));
    }
}
