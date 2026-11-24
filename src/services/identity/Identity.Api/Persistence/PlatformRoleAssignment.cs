namespace Identity.Api.Persistence;

public sealed class PlatformRoleAssignment
{
    private PlatformRoleAssignment()
    {
    }

    public Guid UserId { get; private set; }

    public string Role { get; private set; } = string.Empty;

    public DateTimeOffset AssignedAt { get; private set; }

    public static PlatformRoleAssignment Create(Guid userId, string role, DateTimeOffset assignedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(role);
        return new PlatformRoleAssignment { UserId = userId, Role = role, AssignedAt = assignedAt };
    }
}
