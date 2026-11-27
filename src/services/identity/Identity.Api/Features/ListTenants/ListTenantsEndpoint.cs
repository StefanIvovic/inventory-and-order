using BuildingBlocks.AspNetCore.Authentication;
using Wolverine;
using Wolverine.Http;

namespace Identity.Api.Features.ListTenants;

public static class ListTenantsEndpoint
{
    [WolverineGet("/tenants")]
    [PlatformOnly]
    public static Task<TenantList> Get(
        IMessageBus bus,
        CancellationToken cancellationToken,
        string? q = null,
        int size = ListTenants.DefaultSize,
        string? after = null,
        bool includeTotal = false) =>
        bus.InvokeAsync<TenantList>(new ListTenants(q, size, after, includeTotal), cancellationToken);
}
