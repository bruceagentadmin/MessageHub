namespace MessageHub.Core.Models;

/// <summary>
/// 訊息日誌條目 — 記錄單一訊息事件的完整稽核資訊，供控制中心查詢與展示。
/// </summary>
/// <remarks>
/// 由 <c>InMemoryMessageLogStore</c> 儲存，並透過 <c>GET /api/control/logs</c> 端點查詢。
/// 同時涵蓋入站（<see cref="MessageDirection.Inbound"/>）與出站（<see cref="MessageDirection.Outbound"/>）
/// 以及系統事件（<see cref="MessageDirection.System"/>）的日誌記錄。
/// </remarks>
/// <param name="Id">日誌條目的唯一識別碼（GUID 格式）。</param>
/// <param name="Timestamp">事件發生的時間戳記（含時區資訊）。</param>
/// <param name="TenantId">所屬租戶的識別碼。</param>
/// <param name="Channel">相關頻道的識別字串，例如 <c>telegram</c>、<c>line</c>。</param>
/// <param name="Direction">訊息流向，參見 <see cref="MessageDirection"/>。</param>
/// <param name="Status">訊息投遞狀態，參見 <see cref="DeliveryStatus"/>。</param>
/// <param name="TargetId">目標聊天室或使用者的識別碼。</param>
/// <param name="TargetDisplayName">目標的顯示名稱，可為 <see langword="null"/>（若平台未提供）。</param>
/// <param name="Content">訊息的純文字內容摘要。</param>
/// <param name="Source">觸發此訊息事件的來源描述，例如 <c>telegram webhook</c>、<c>control center</c>。</param>
/// <param name="Details">附加的詳細資訊，例如發送者 ID 或錯誤說明，可為 <see langword="null"/>。</param>
public sealed record MessageLogEntry(
    Guid Id,
    DateTimeOffset Timestamp,
    string TenantId,
    string Channel,
    MessageDirection Direction,
    DeliveryStatus Status,
    string TargetId,
    string? TargetDisplayName,
    string Content,
    string Source,
    string? Details = null);
