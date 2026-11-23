using BuildingBlocks.Core.Identity;
using BuildingBlocks.Core.Permissions;

namespace BuildingBlocks.AspNetCore.Permissions;

internal sealed class PermissionManager(ICurrentUser user, IPermissionProvider provider, PermissionCache cache) : IPermissionManager
{
    private Task<IReadOnlySet<string>>? resolved;

    public Task<IReadOnlySet<string>> GetGrantedAsync(CancellationToken cancellationToken = default) =>
        resolved ??= ResolveAsync(cancellationToken);

    public async Task<bool> HasAsync(string permission, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);
        return (await GetGrantedAsync(cancellationToken)).Contains(permission);
    }

    public async Task<bool> HasAllAsync(IEnumerable<string> permissions, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(permissions);
        var granted = await GetGrantedAsync(cancellationToken);
        return permissions.All(granted.Contains);
    }

    public async Task<bool> HasAnyAsync(IEnumerable<string> permissions, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(permissions);
        var granted = await GetGrantedAsync(cancellationToken);
        return permissions.Any(granted.Contains);
    }

    public Task EnsureAsync(string permission, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);
        return EnsureAllAsync([permission], cancellationToken);
    }

    public async Task EnsureAllAsync(IEnumerable<string> permissions, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(permissions);
        var requested = permissions.ToList();
        var granted = await GetGrantedAsync(cancellationToken);
        var missing = requested.Where(p => !granted.Contains(p)).ToList();
        if (missing.Count > 0)
        {
            throw new PermissionDeniedException(missing);
        }
    }

    private async Task<IReadOnlySet<string>> ResolveAsync(CancellationToken cancellationToken)
    {
        if (!user.IsAuthenticated)
        {
            return PermissionCache.EmptySet;
        }

        var tenantId = user.TenantId;
        var userId = user.UserId;
        if (cache.TryGet(tenantId, userId, out var cached))
        {
            return cached;
        }

        try
        {
            var loaded = await provider.GetPermissionsAsync(user, cancellationToken);
            var granted = loaded.Count == 0
                ? PermissionCache.EmptySet
                : new HashSet<string>(loaded, StringComparer.Ordinal);
            cache.Store(tenantId, userId, granted);
            return granted;
        }
        catch
        {
            resolved = null;
            throw;
        }
    }
}
