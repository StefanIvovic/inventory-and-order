using BuildingBlocks.Core.Tenancy;
using BuildingBlocks.EntityFrameworkCore;
using Identity.Api.Authorization;
using Identity.Api.Users;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenIddict.EntityFrameworkCore.Models;

namespace Identity.Api.Persistence;

public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options, ITenantContext tenantContext)
    : TenantDbContext(options, tenantContext), IDataProtectionKeyContext
{
    private const int NameMaxLength = 256;

    public DbSet<RoleAssignment> RoleAssignments => Set<RoleAssignment>();

    public DbSet<AppUser> Users => Set<AppUser>();

    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<RoleAssignment>(entity =>
        {
            var roles = string.Join(", ", IdentityRoles.All.Keys.Order(StringComparer.Ordinal).Select(name => $"'{name}'"));
            entity.ToTable("role_assignments", table => table.HasCheckConstraint("ck_role_assignments_role", $"\"Role\" IN ({roles})"));
            entity.HasKey(a => new { a.TenantId, a.UserId, a.Role });
            entity.Property(a => a.Role).IsRequired().HasMaxLength(RoleAssignment.RoleMaxLength);
            entity.Property(a => a.AssignedAt).IsRequired();
        });

        ConfigureUsers(modelBuilder);
        modelBuilder.Entity<DataProtectionKey>().ToTable("data_protection_keys");

        modelBuilder.UseOpenIddict<Guid>();
        modelBuilder.Entity<OpenIddictEntityFrameworkCoreApplication<Guid>>().ToTable("openiddict_applications");
        modelBuilder.Entity<OpenIddictEntityFrameworkCoreAuthorization<Guid>>().ToTable("openiddict_authorizations");
        modelBuilder.Entity<OpenIddictEntityFrameworkCoreScope<Guid>>().ToTable("openiddict_scopes");
        modelBuilder.Entity<OpenIddictEntityFrameworkCoreToken<Guid>>().ToTable("openiddict_tokens");

        base.OnModelCreating(modelBuilder);
    }

    private static void ConfigureUsers(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUser>(user =>
        {
            user.ToTable("identity_users", table => table.HasCheckConstraint(
                "ck_identity_users_type_tenant",
                $"(\"UserType\" = '{UserTypes.Platform}' AND \"TenantId\" IS NULL) OR (\"UserType\" IN ('{UserTypes.Staff}', '{UserTypes.Customer}') AND \"TenantId\" IS NOT NULL)"));
            user.HasKey(u => u.Id);
            user.HasIndex(u => u.NormalizedUserName).IsUnique();
            user.HasIndex(u => u.NormalizedEmail).IsUnique();
            user.Property(u => u.ConcurrencyStamp).IsConcurrencyToken();
            user.Property(u => u.UserName).HasMaxLength(NameMaxLength);
            user.Property(u => u.NormalizedUserName).HasMaxLength(NameMaxLength);
            user.Property(u => u.Email).HasMaxLength(NameMaxLength);
            user.Property(u => u.NormalizedEmail).HasMaxLength(NameMaxLength);
            user.Property(u => u.UserType).IsRequired().HasMaxLength(20);
            user.Property(u => u.TenantId).HasMaxLength(TenantFilter.TenantIdMaxLength);
            user.HasMany<IdentityUserClaim<Guid>>().WithOne().HasForeignKey(c => c.UserId).IsRequired();
            user.HasMany<IdentityUserLogin<Guid>>().WithOne().HasForeignKey(l => l.UserId).IsRequired();
            user.HasMany<IdentityUserToken<Guid>>().WithOne().HasForeignKey(t => t.UserId).IsRequired();
        });

        modelBuilder.Entity<IdentityUserClaim<Guid>>(claim =>
        {
            claim.ToTable("identity_user_claims");
            claim.HasKey(c => c.Id);
        });

        modelBuilder.Entity<IdentityUserLogin<Guid>>(login =>
        {
            login.ToTable("identity_user_logins");
            login.HasKey(l => new { l.LoginProvider, l.ProviderKey });
        });

        modelBuilder.Entity<IdentityUserToken<Guid>>(token =>
        {
            token.ToTable("identity_user_tokens");
            token.HasKey(t => new { t.UserId, t.LoginProvider, t.Name });
        });
    }
}
