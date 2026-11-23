using BuildingBlocks.Core.Permissions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace BuildingBlocks.AspNetCore.Permissions;

internal sealed class PermissionCache(IMemoryCache cache, TimeProvider clock, IOptions<PermissionOptions> options) : IPermissionCache
{
    public static string KeyFor(string? tenantId, Guid userId) => $"perm:{tenantId ?? string.Empty}:{userId:N}";

    public bool TryGet(string? tenantId, Guid userId, out IReadOnlySet<string> granted)
    {
        if (cache.TryGetValue<Entry>(KeyFor(tenantId, userId), out var entry) &&
            entry is not null &&
            clock.GetUtcNow() < entry.ExpiresAt)
        {
            granted = entry.Granted;
            return true;
        }

        granted = EmptySet;
        return false;
    }

    public void Store(string? tenantId, Guid userId, IReadOnlySet<string> granted)
    {
        var duration = options.Value.CacheDuration;
        cache.Set(KeyFor(tenantId, userId), new Entry(granted, clock.GetUtcNow() + duration), duration);
    }

    public void Invalidate(string? tenantId, Guid userId) => cache.Remove(KeyFor(tenantId, userId));

    internal static readonly IReadOnlySet<string> EmptySet = new HashSet<string>(StringComparer.Ordinal);

    private sealed record Entry(IReadOnlySet<string> Granted, DateTimeOffset ExpiresAt);
}
