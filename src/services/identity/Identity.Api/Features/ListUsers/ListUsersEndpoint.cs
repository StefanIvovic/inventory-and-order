using Wolverine;
using Wolverine.Http;

namespace Identity.Api.Features.ListUsers;

public static class ListUsersEndpoint
{
    [WolverineGet("/users")]
    public static Task<UserList> Get(
        IMessageBus bus,
        CancellationToken cancellationToken,
        string? q = null,
        string? status = null,
        string? role = null,
        int size = ListUsers.DefaultSize,
        string? after = null,
        bool includeTotal = false) =>
        bus.InvokeAsync<UserList>(new ListUsers(q, status, role, size, after, includeTotal), cancellationToken);
}
