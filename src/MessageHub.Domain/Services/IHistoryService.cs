using MessageHub.Domain.Models;

namespace MessageHub.Domain.Services;

/// <summary>
/// 歷史查詢 Service 介面。
/// </summary>
public interface IHistoryService
{
    Task<PagedResult<MessageLogRecord>> QueryLogsAsync(MessageLogQuery query, CancellationToken ct = default);
}
