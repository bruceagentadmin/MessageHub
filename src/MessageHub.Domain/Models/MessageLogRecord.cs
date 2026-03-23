namespace MessageHub.Domain.Models;

/// <summary>
/// 訊息日誌紀錄 — Domain 層的日誌資料模型。
/// 與 Core 的 MessageLogEntry 欄位對等，但不產生跨層相依。
/// </summary>
public sealed record MessageLogRecord(
    Guid Id,
    DateTimeOffset Timestamp,
    string TenantId,
    string Channel,
    int Direction,
    int Status,
    string TargetId,
    string? TargetDisplayName,
    string Content,
    string Source,
    string? Details = null);
