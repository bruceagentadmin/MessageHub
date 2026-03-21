using System.Collections.Concurrent;
using MessageHub.Core;

namespace MessageHub.Infrastructure;

public sealed class RecentTargetStore : IRecentTargetStore
{
    private readonly ConcurrentDictionary<string, RecentTargetInfo> _targets = new(StringComparer.OrdinalIgnoreCase);

    public Task SetLastTargetAsync(string channel, string targetId, string? displayName = null, CancellationToken cancellationToken = default)
    {
        _targets[channel] = new RecentTargetInfo(channel, targetId, displayName, DateTimeOffset.UtcNow);
        return Task.CompletedTask;
    }

    public Task<RecentTargetInfo?> GetLastTargetAsync(string channel, CancellationToken cancellationToken = default)
    {
        _targets.TryGetValue(channel, out var value);
        return Task.FromResult(value);
    }
}
