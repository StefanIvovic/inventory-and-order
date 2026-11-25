using BuildingBlocks.AspNetCore.Authentication;
using Wolverine;
using Wolverine.Http;

namespace Identity.Api.Features.CreateTenant;

public static class CreateTenantEndpoint
{
    [WolverinePost("/tenants")]
    [PlatformOnly]
    public static async Task<IResult> Post(CreateTenant command, IMessageBus bus, CancellationToken cancellationToken)
    {
        var created = await bus.InvokeAsync<CreateTenantResponse>(command, cancellationToken);
        return Results.Created($"/tenants/{created.Id}", created);
    }
}
