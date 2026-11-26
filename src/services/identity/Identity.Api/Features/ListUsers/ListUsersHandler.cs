using System.Diagnostics.CodeAnalysis;
using BuildingBlocks.Core.Handlers;
using BuildingBlocks.Validation;
using Identity.Api.Authorization;
using Identity.Api.Persistence;
using Identity.Api.Users;
using Microsoft.EntityFrameworkCore;

namespace Identity.Api.Features.ListUsers;

public sealed class ListUsersHandler(IRequestContext context, IdentityDbContext db, ICommandValidation<ListUsers> validation)
    : CommandHandler<IdentityDbContext>(context, db)
{
    [SuppressMessage("Globalization", "CA1309", Justification = "Translated to a SQL comparison in the index collation.")]
    [SuppressMessage("Globalization", "CA1310", Justification = "Translated to a SQL prefix match.")]
    public async Task<UserList> Handle(ListUsers query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        await Permissions.EnsureAsync(IdentityPermissions.UsersView, cancellationToken);
        await validation.EnsureValidAsync(query, cancellationToken);
        var after = UserCursor.Decode(query.After);

        var tenantId = Tenant.TenantId;
        var users = Db.Users.AsNoTracking().Where(u => u.TenantId == tenantId && u.UserTypeId == EF.Constant(UserTypes.StaffValue.Id));

        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var prefix = query.Q.Trim().ToUpperInvariant();
            users = users.Where(u => u.NormalizedEmail!.StartsWith(prefix));
        }

        users = query.Status switch
        {
            StaffStatuses.Active => users.Where(u => u.DisabledAt == null),
            StaffStatuses.Disabled => users.Where(u => u.DisabledAt != null),
            _ => users,
        };

        if (query.Role is { } role)
        {
            users = users.Where(u => Db.RoleAssignments.Any(a => a.UserId == u.Id && a.Role == role));
        }

        var total = query.IncludeTotal ? await users.CountAsync(cancellationToken) : (int?)null;

        if (after is not null)
        {
            var email = after.Email;
            var id = after.Id;
            users = users.Where(u => string.Compare(u.NormalizedEmail, email) >= 0
                && (string.Compare(u.NormalizedEmail, email) > 0 || u.Id.CompareTo(id) > 0));
        }

        var rows = await users
            .OrderBy(u => u.NormalizedEmail)
            .ThenBy(u => u.Id)
            .Take(query.Size + 1)
            .Select(u => new
            {
                u.Id,
                u.Email,
                u.NormalizedEmail,
                u.DisabledAt,
                u.CreatedAt,
                Roles = Db.RoleAssignments.Where(a => a.UserId == u.Id).OrderBy(a => a.Role).Select(a => a.Role).ToList(),
            })
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > query.Size;
        var page = hasMore ? rows[..query.Size] : rows;
        var canManage = await Permissions.HasAsync(IdentityPermissions.UsersManage, cancellationToken);
        var canAssign = await Permissions.HasAsync(IdentityPermissions.RolesAssign, cancellationToken);

        var items = page.Select(u => new UserItem(
                u.Id,
                u.Email!,
                UserTypes.Staff,
                u.DisabledAt is not null,
                u.Roles,
                u.CreatedAt,
                new UserActions(
                    canManage && u.DisabledAt is null && u.Id != User.UserId,
                    canAssign && u.DisabledAt is null)))
            .ToList();
        var next = hasMore ? new UserCursor(page[^1].NormalizedEmail!, page[^1].Id).Encode() : null;

        return new UserList(items, next, total, new UserListActions(canManage));
    }
}
