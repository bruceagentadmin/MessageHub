using MessageHub.Core.Models;

namespace MessageHub.Core;

/// <summary>
/// 最近互動對象儲存介面
/// </summary>
public interface IRecentTargetStore
{
    Task SetLastTargetAsync(string channel, string targetId, string? displayName = null, CancellationToken cancellationToken = default);
    Task<RecentTargetInfo?> GetLastTargetAsync(string channel, CancellationToken cancellationToken = default);
}
