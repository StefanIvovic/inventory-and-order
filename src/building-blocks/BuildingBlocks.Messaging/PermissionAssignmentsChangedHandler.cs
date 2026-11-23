using BuildingBlocks.Core.Permissions;

namespace BuildingBlocks.Messaging;

public static class PermissionAssignmentsChangedHandler
{
    public static void Handle(PermissionAssignmentsChanged message, IPermissionCache cache)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(cache);
        cache.Invalidate(message.TenantId, message.UserId);
    }
}
