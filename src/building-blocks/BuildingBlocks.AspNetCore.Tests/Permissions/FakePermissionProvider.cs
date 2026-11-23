using System.Collections.Concurrent;
using BuildingBlocks.Core.Handlers;
using BuildingBlocks.Core.Identity;
using BuildingBlocks.Core.Permissions;

namespace BuildingBlocks.AspNetCore.Tests.Permissions;

public sealed class PermissionStore
{
    private readonly ConcurrentDictionary<(string? Tenant, Guid User), IReadOnlySet<string>> grants = new();
    private int calls;
    private int failNext;

    public int Calls => calls;

    public void Grant(Guid userId, string? tenantId, params string[] permissions) =>
        grants[(tenantId, userId)] = permissions.ToHashSet(StringComparer.Ordinal);

    public void FailNextCall() => Interlocked.Exchange(ref failNext, 1);

    public IReadOnlySet<string> Load(ICurrentUser user)
    {
        Interlocked.Increment(ref calls);
        if (Interlocked.Exchange(ref failNext, 0) == 1)
        {
            throw new InvalidOperationException("permission store unavailable");
        }

        return grants.TryGetValue((user.TenantId, user.UserId), out var granted)
            ? granted
            : new HashSet<string>(StringComparer.Ordinal);
    }
}

public sealed class FakePermissionProvider(PermissionStore store) : IPermissionProvider
{
    public Task<IReadOnlySet<string>> GetPermissionsAsync(ICurrentUser user, CancellationToken cancellationToken) =>
        Task.FromResult(store.Load(user));
}

public sealed class SampleDb
{
    public Guid Id { get; } = Guid.NewGuid();
}

public sealed record HandlerReport(Guid UserId, string? TenantId, DateTimeOffset Now, bool CanAdjust, Guid DbId);

public sealed class SampleHandler(IRequestContext context, SampleDb db) : CommandHandler<SampleDb>(context, db)
{
    public async Task<HandlerReport> Handle(CancellationToken cancellationToken) =>
        new(User.UserId, Tenant.TenantId, Clock.GetUtcNow(), await Permissions.HasAsync("inventory.adjust", cancellationToken), Db.Id);
}
