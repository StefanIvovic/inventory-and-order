using BuildingBlocks.Core.Tenancy;
using BuildingBlocks.EntityFrameworkCore;
using Identity.Api.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Identity.Api.Persistence;

public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options, ITenantContext tenantContext)
    : TenantDbContext(options, tenantContext)
{
    public DbSet<RoleAssignment> RoleAssignments => Set<RoleAssignment>();

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

        base.OnModelCreating(modelBuilder);
    }
}
