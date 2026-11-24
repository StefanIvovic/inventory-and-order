using BuildingBlocks.Core.Handlers;
using Identity.Api.Authorization;
using Identity.Api.Persistence;

namespace Identity.Api.Features.ListRoles;

public sealed class GetRolesHandler(IRequestContext context, IdentityDbContext db) : CommandHandler<IdentityDbContext>(context, db)
{
    public async Task<RolesResponse> Handle(GetRoles query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        await Permissions.EnsureAsync(IdentityPermissions.RolesView, cancellationToken);

        var roles = IdentityRoles.Tenant
            .OrderBy(role => role.Key, StringComparer.Ordinal)
            .Select(role => new RoleDto(role.Key, role.Value.Order(StringComparer.Ordinal).ToList()))
            .ToList();

        return new RolesResponse(roles, await Permissions.HasAsync(IdentityPermissions.RolesAssign, cancellationToken));
    }
}
