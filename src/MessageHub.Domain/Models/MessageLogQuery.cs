namespace MessageHub.Domain.Models;

/// <summary>
/// 訊息日誌查詢參數 — 支援多條件篩選與分頁。
/// </summary>
public sealed record MessageLogQuery(
    string? Channel = null,
    int? Direction = null,
    int? Status = null,
    string? TargetId = null,
    string? SenderId = null,
    string? ContentKeyword = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    int Page = 1,
    int PageSize = 50);
