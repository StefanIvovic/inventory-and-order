using BuildingBlocks.Core.Identity;
using BuildingBlocks.Core.Permissions;
using BuildingBlocks.Core.Tenancy;

namespace BuildingBlocks.Core.Handlers;

public interface IRequestContext
{
    ICurrentUser User { get; }

    ITenantContext Tenant { get; }

    IPermissionManager Permissions { get; }

    TimeProvider Clock { get; }
}
