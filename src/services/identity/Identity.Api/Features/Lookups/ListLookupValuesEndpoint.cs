using BuildingBlocks.AspNetCore.Authentication;
using BuildingBlocks.Lookups;
using Wolverine;
using Wolverine.Http;

namespace Identity.Api.Features.Lookups;

public static class ListLookupValuesEndpoint
{
    [WolverineGet("/lookups/{typeCode}/values")]
    [SignedIn]
    [LookupCaching]
    public static Task<LookupValuePage> Get(
        string typeCode,
        IMessageBus bus,
        CancellationToken cancellationToken,
        string? q = null,
        Guid[]? ids = null,
        string[]? codes = null,
        int size = LookupValueQuery.DefaultSize,
        string? after = null) =>
        bus.InvokeAsync<LookupValuePage>(new ListLookupValues(typeCode, q, ids, codes, size, after), cancellationToken);
}
