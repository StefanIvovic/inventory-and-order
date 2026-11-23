namespace BuildingBlocks.Core.Permissions;

public interface IPermissionCache
{
    void Invalidate(string? tenantId, Guid userId);
}
