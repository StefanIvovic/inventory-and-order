using BuildingBlocks.Core.Handlers;
using Product.Api.Authorization;
using Product.Api.Persistence;

namespace Product.Api.Features.ListRoles;

public sealed class GetRolesHandler(IRequestContext context, ProductDbContext db) : CommandHandler<ProductDbContext>(context, db)
{
    public async Task<RolesResponse> Handle(GetRoles query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        await Permissions.EnsureAsync(ProductPermissions.RolesView, cancellationToken);

        var roles = ProductRoles.Tenant
            .Select(role => new RoleDto(
                role.Key,
                ProductRoles.DisplayNames[role.Key],
                [.. role.Value
                    .Select(permission => new PermissionDto(permission, ProductPermissions.DisplayNames[permission]))
                    .OrderBy(permission => permission.DisplayName, StringComparer.Ordinal)]))
            .OrderBy(role => role.DisplayName, StringComparer.Ordinal)
            .ToList();

        return new RolesResponse(roles, false);
    }
}
