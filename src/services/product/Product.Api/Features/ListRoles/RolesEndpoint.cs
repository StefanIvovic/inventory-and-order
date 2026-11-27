using Wolverine;
using Wolverine.Http;

namespace Product.Api.Features.ListRoles;

public static class RolesEndpoint
{
    [WolverineGet("/roles")]
    public static Task<RolesResponse> Get(IMessageBus bus, CancellationToken cancellationToken) =>
        bus.InvokeAsync<RolesResponse>(new GetRoles(), cancellationToken);
}
