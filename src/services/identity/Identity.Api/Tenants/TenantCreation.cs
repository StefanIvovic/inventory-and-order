using BuildingBlocks.Core.Tenancy;
using Identity.Api.Authorization;
using Identity.Api.Persistence;
using Identity.Api.Users;
using Identity.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Identity.Api.Tenants;

public sealed record NewTenant(Tenant Tenant, AppUser Admin, TenantCreated Created, UserRegistered Registered);

public sealed class TenantCreation(IdentityDbContext db, HandlerUserManager users, TenantContext tenantContext)
{
    public async Task<NewTenant> CreateAsync(
        string slug,
        string name,
        string adminEmail,
        string adminPassword,
        DateTimeOffset now,
        string emailField,
        string passwordField,
        CancellationToken cancellationToken)
    {
        if (await db.Tenants.AnyAsync(t => t.Slug == slug, cancellationToken))
        {
            throw TenantErrors.SlugTakenError();
        }

        var tenant = Tenant.Create(slug, name, now);
        tenantContext.Set(tenant.Id);
        db.Tenants.Add(tenant);

        var admin = AppUser.ForTenant(adminEmail.Trim(), tenant.Id, UserTypes.Staff, now);
        IdentityResultErrors.ThrowIfFailed(await users.CreateAsync(admin, adminPassword), emailField, passwordField);
        db.RoleAssignments.Add(RoleAssignment.Create(admin.Id, IdentityRoles.Administrator, now));

        return new NewTenant(
            tenant,
            admin,
            new TenantCreated(tenant.Id, admin.Id),
            new UserRegistered(admin.Id, tenant.Id, admin.UserType, admin.Email!));
    }
}
