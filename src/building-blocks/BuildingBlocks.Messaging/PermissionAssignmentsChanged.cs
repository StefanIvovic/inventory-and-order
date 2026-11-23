using Wolverine.Attributes;

namespace BuildingBlocks.Messaging;

[MessageIdentity("building-blocks.permission-assignments-changed.v1")]
public sealed record PermissionAssignmentsChanged
{
    public PermissionAssignmentsChanged(string? tenantId, Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("A user id is required.", nameof(userId));
        }

        TenantId = string.IsNullOrWhiteSpace(tenantId) ? null : tenantId;
        UserId = userId;
    }

    public string? TenantId { get; }

    public Guid UserId { get; }
}
