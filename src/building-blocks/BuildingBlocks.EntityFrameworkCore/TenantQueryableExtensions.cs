using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.EntityFrameworkCore;

public static class TenantQueryableExtensions
{
    public static IQueryable<T> AcrossAllTenants<T>(this IQueryable<T> source)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.IgnoreQueryFilters([TenantFilter.Name]);
    }
}
