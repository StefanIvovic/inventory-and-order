using BuildingBlocks.Core.Tenancy;

namespace Product.Api.Persistence;

public sealed class RoleAssignment : ITenantOwned
{
    public const int RoleMaxLength = 100;

    private RoleAssignment()
    {
    }

    public string TenantId { get; private set; } = string.Empty;

    public Guid UserId { get; private set; }

    public string Role { get; private set; } = string.Empty;

    public DateTimeOffset AssignedAt { get; private set; }

    public static RoleAssignment Create(Guid userId, string role, DateTimeOffset assignedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(role);
        return new RoleAssignment { UserId = userId, Role = role, AssignedAt = assignedAt };
    }
}
