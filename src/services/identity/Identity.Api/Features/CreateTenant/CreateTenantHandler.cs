using BuildingBlocks.Core.Handlers;
using BuildingBlocks.Validation;
using Identity.Api.Authorization;
using Identity.Api.Persistence;
using Identity.Api.Tenants;
using Wolverine;

namespace Identity.Api.Features.CreateTenant;

public sealed class CreateTenantHandler(
    IRequestContext context,
    IdentityDbContext db,
    ICommandValidation<CreateTenant> validation,
    TenantCreation creation) : CommandHandler<IdentityDbContext>(context, db)
{
    public async Task<(CreateTenantResponse, OutgoingMessages)> Handle(CreateTenant command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        await Permissions.EnsureAsync(IdentityPermissions.TenantsManage, cancellationToken);
        await validation.EnsureValidAsync(command, cancellationToken);

        var created = await creation.CreateAsync(
            command.Slug,
            command.Name,
            command.AdminEmail,
            command.AdminPassword,
            Clock.GetUtcNow(),
            nameof(CreateTenant.AdminEmail),
            nameof(CreateTenant.AdminPassword),
            cancellationToken);

        var tenantId = created.Tenant.Id;
        return (
            new CreateTenantResponse(tenantId, created.Tenant.Slug, created.Admin.Id),
            [created.Created.WithTenantId(tenantId), created.Registered.WithTenantId(tenantId)]);
    }
}
