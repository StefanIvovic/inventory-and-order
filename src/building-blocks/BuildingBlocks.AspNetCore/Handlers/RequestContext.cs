using BuildingBlocks.Core.Handlers;
using BuildingBlocks.Core.Identity;
using BuildingBlocks.Core.Permissions;
using BuildingBlocks.Core.Tenancy;

namespace BuildingBlocks.AspNetCore.Handlers;

internal sealed class RequestContext(
    ICurrentUser user,
    ITenantContext tenant,
    IPermissionManager permissions,
    TimeProvider clock) : IRequestContext
{
    public ICurrentUser User { get; } = user;

    public ITenantContext Tenant { get; } = tenant;

    public IPermissionManager Permissions { get; } = permissions;

    public TimeProvider Clock { get; } = clock;
}
