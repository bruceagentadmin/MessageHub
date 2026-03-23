using MessageHub.Domain.Models;
using MessageHub.Domain.Repositories;

namespace MessageHub.Domain.Services;

/// <summary>
/// 歷史查詢 Service 實作 — 委派 Repository 進行進階查詢。
/// </summary>
public sealed class HistoryService(IMessageLogRepository repository) : IHistoryService
{
    public Task<PagedResult<MessageLogRecord>> QueryLogsAsync(MessageLogQuery query, CancellationToken ct = default)
        => repository.QueryAsync(query, ct);
}
