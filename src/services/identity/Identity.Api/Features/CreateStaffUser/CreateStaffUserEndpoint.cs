using Wolverine;
using Wolverine.Http;

namespace Identity.Api.Features.CreateStaffUser;

public static class CreateStaffUserEndpoint
{
    [WolverinePost("/users")]
    public static async Task<IResult> Post(CreateStaffUser command, IMessageBus bus, CancellationToken cancellationToken)
    {
        var created = await bus.InvokeAsync<CreateStaffUserResponse>(command, cancellationToken);
        return Results.Created($"/users/{created.Id}", created);
    }
}
