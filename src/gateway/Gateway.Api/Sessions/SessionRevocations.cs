using System.Collections.Concurrent;

namespace Gateway.Api.Sessions;

public sealed class SessionRevocations(TimeProvider time)
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> revoked = new(StringComparer.Ordinal);

    public void Revoke(string sessionId, DateTimeOffset? until)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);

        var now = time.GetUtcNow();
        foreach (var (key, expires) in revoked)
        {
            if (expires <= now)
            {
                revoked.TryRemove(key, out _);
            }
        }

        revoked[sessionId] = until ?? now + SessionSetup.SessionLifetime;
    }

    public bool IsRevoked(string? sessionId) =>
        sessionId is not null && revoked.TryGetValue(sessionId, out var expires) && expires > time.GetUtcNow();
}
