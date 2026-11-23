namespace BuildingBlocks.Core.Identity;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    Guid UserId { get; }

    string? TenantId { get; }

    UserType UserType { get; }
}
