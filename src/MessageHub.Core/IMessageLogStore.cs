using MessageHub.Core.Models;

namespace MessageHub.Core;

/// <summary>
/// 訊息紀錄儲存介面
/// </summary>
public interface IMessageLogStore
{
    Task AddAsync(MessageLogEntry entry, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MessageLogEntry>> GetRecentAsync(int count, CancellationToken cancellationToken = default);
}
