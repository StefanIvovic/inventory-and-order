using BuildingBlocks.AspNetCore.Authentication;
using Wolverine;
using Wolverine.Http;

namespace Identity.Api.Features.ListTenants;

public static class ListTenantsEndpoint
{
    [WolverineGet("/tenants")]
    [PlatformOnly]
    public static Task<TenantList> Get(IMessageBus bus, CancellationToken cancellationToken) =>
        bus.InvokeAsync<TenantList>(new ListTenants(), cancellationToken);
}
