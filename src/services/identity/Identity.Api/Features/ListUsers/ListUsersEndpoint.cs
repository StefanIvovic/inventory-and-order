using Wolverine;
using Wolverine.Http;

namespace Identity.Api.Features.ListUsers;

public static class ListUsersEndpoint
{
    [WolverineGet("/users")]
    public static Task<UserList> Get(IMessageBus bus, CancellationToken cancellationToken) =>
        bus.InvokeAsync<UserList>(new ListUsers(), cancellationToken);
}
