using Wolverine;
using Wolverine.Http;

namespace Identity.Api.Features.AssignRole;

public static class RoleAssignmentEndpoints
{
    [WolverinePut("/users/{id}/roles/{role}")]
    public static async Task<IResult> Put(Guid id, string role, IMessageBus bus, CancellationToken cancellationToken)
    {
        await bus.InvokeAsync(new AssignRole(id, role), cancellationToken);
        return Results.NoContent();
    }

    [WolverineDelete("/users/{id}/roles/{role}")]
    public static async Task<IResult> Delete(Guid id, string role, IMessageBus bus, CancellationToken cancellationToken)
    {
        await bus.InvokeAsync(new UnassignRole(id, role), cancellationToken);
        return Results.NoContent();
    }
}
