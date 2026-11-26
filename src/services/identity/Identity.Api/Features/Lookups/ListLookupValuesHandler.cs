using BuildingBlocks.Core.Handlers;
using BuildingBlocks.Lookups;
using Identity.Api.Persistence;

namespace Identity.Api.Features.Lookups;

public sealed class ListLookupValuesHandler(IRequestContext context, IdentityDbContext db) : CommandHandler<IdentityDbContext>(context, db)
{
    public Task<LookupValuePage> Handle(ListLookupValues query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        return Db.ReadValuesAsync(
            new LookupValueQuery(query.TypeCode, query.Q, query.Ids, query.Codes, query.Size, query.After),
            cancellationToken);
    }
}
