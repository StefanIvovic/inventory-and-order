using BuildingBlocks.Core.Tenancy;
using BuildingBlocks.EntityFrameworkCore;
using Identity.Api.Authorization;
using Identity.Api.Tenants;
using Identity.Api.Users;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenIddict.EntityFrameworkCore.Models;

namespace Identity.Api.Persistence;

public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options, ITenantContext tenantContext)
    : TenantDbContext(options, tenantContext), IDataProtectionKeyContext
{
    public const string StaffEmailIndex = "ux_identity_users_staff_email";
    public const string TenantSlugIndex = "ux_tenants_slug";
    public const string UserNameIndex = "IX_identity_users_NormalizedUserName";
    public const string CustomerEmailIndex = "ux_identity_users_customer_email";
    private const int NameMaxLength = 256;
    private const int UserNameMaxLength = 320;

    public DbSet<RoleAssignment> RoleAssignments => Set<RoleAssignment>();

    public DbSet<PlatformRoleAssignment> PlatformRoleAssignments => Set<PlatformRoleAssignment>();

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<AppUser> Users => Set<AppUser>();

    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<RoleAssignment>(entity =>
        {
            var roles = RoleList(IdentityRoles.Tenant.Keys);
            entity.ToTable("role_assignments", table => table.HasCheckConstraint("ck_role_assignments_role", $"\"Role\" IN ({roles})"));
            entity.HasKey(a => new { a.TenantId, a.UserId, a.Role });
            entity.Property(a => a.Role).IsRequired().HasMaxLength(RoleAssignment.RoleMaxLength);
            entity.Property(a => a.AssignedAt).IsRequired();
        });

        modelBuilder.Entity<PlatformRoleAssignment>(entity =>
        {
            entity.ToTable("platform_role_assignments", table => table.HasCheckConstraint(
                "ck_platform_role_assignments_role", $"\"Role\" IN ({RoleList(IdentityRoles.Platform.Keys)})"));
            entity.HasKey(a => new { a.UserId, a.Role });
            entity.Property(a => a.Role).IsRequired().HasMaxLength(RoleAssignment.RoleMaxLength);
            entity.Property(a => a.AssignedAt).IsRequired();
        });

        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.ToTable("tenants", table => table.HasCheckConstraint("ck_tenants_slug", $"\"Slug\" ~ '{Tenant.SlugPattern}'"));
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Id).HasMaxLength(Tenant.IdLength);
            entity.Property(t => t.Slug).IsRequired().HasMaxLength(Tenant.SlugMaxLength);
            entity.HasIndex(t => t.Slug).IsUnique().HasDatabaseName(TenantSlugIndex);
            entity.Property(t => t.Name).IsRequired().HasMaxLength(Tenant.NameMaxLength);
            entity.Property(t => t.CreatedAt).IsRequired();
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
            user.HasIndex(u => u.NormalizedEmail).IsUnique().HasDatabaseName(StaffEmailIndex).HasFilter($"\"UserType\" <> '{UserTypes.Customer}'");
            user.Property(u => u.CreatedAt).IsRequired();
            user.Property(u => u.DisabledAt);
            user.Property(u => u.ConcurrencyStamp).IsConcurrencyToken();
            user.Property(u => u.UserName).HasMaxLength(UserNameMaxLength);
            user.Property(u => u.NormalizedUserName).HasMaxLength(UserNameMaxLength);
            user.HasIndex(u => new { u.TenantId, u.NormalizedEmail }).IsUnique().HasDatabaseName(CustomerEmailIndex).HasFilter($"\"UserType\" = '{UserTypes.Customer}'");
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

    private static string RoleList(IEnumerable<string> roles) =>
        string.Join(", ", roles.Order(StringComparer.Ordinal).Select(name => $"'{name}'"));
}
