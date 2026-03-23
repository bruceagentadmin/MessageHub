using MessageHub.Domain.Models;

namespace MessageHub.Domain.Repositories;

/// <summary>
/// 訊息日誌 Repository — 提供訊息日誌的持久化存取。
/// </summary>
public interface IMessageLogRepository
{
    /// <summary>新增一筆日誌。</summary>
    Task AddAsync(MessageLogRecord record, CancellationToken ct = default);

    /// <summary>取得最近 N 筆日誌（供即時 Log 面板用，相容現有行為）。</summary>
    Task<IReadOnlyList<MessageLogRecord>> GetRecentAsync(int count, CancellationToken ct = default);

    /// <summary>進階查詢（帶篩選 + 分頁）。</summary>
    Task<PagedResult<MessageLogRecord>> QueryAsync(MessageLogQuery query, CancellationToken ct = default);
}
