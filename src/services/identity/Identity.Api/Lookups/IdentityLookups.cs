using BuildingBlocks.Lookups;
using Identity.Api.Users;

namespace Identity.Api.Lookups;

public static class IdentityLookups
{
    public static readonly LookupCatalog Catalog = new(UserTypes.Type, StaffStatuses.Type);
}
