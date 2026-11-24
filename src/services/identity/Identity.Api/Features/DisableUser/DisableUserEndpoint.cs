using Wolverine;
using Wolverine.Http;

namespace Identity.Api.Features.DisableUser;

public static class DisableUserEndpoint
{
    [WolverinePost("/users/{id:guid}/disable")]
    public static async Task<IResult> Post(Guid id, IMessageBus bus, CancellationToken cancellationToken)
    {
        await bus.InvokeAsync(new DisableUser(id), cancellationToken);
        return Results.NoContent();
    }
}
