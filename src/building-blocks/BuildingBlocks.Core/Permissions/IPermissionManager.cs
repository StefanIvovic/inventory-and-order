namespace BuildingBlocks.Core.Permissions;

public interface IPermissionManager
{
    Task<bool> HasAsync(string permission, CancellationToken cancellationToken = default);

    Task<bool> HasAllAsync(IEnumerable<string> permissions, CancellationToken cancellationToken = default);

    Task<bool> HasAnyAsync(IEnumerable<string> permissions, CancellationToken cancellationToken = default);

    Task EnsureAsync(string permission, CancellationToken cancellationToken = default);

    Task EnsureAllAsync(IEnumerable<string> permissions, CancellationToken cancellationToken = default);

    Task<IReadOnlySet<string>> GetGrantedAsync(CancellationToken cancellationToken = default);
}
