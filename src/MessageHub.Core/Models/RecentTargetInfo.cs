namespace MessageHub.Core.Models;

/// <summary>
/// 最近互動目標資訊 — 記錄某頻道下最近一次互動的目標對象，供發訊時自動填入收件人。
/// </summary>
/// <remarks>
/// 由 <c>RecentTargetStore</c> 維護，當 <see cref="SendMessageRequest.TargetId"/> 為空時，
/// 系統會自動查找對應頻道的最近目標，避免使用者每次手動輸入。
/// </remarks>
/// <param name="Channel">頻道識別字串，例如 <c>telegram</c>、<c>line</c>。</param>
/// <param name="TargetId">最近互動目標的識別碼，通常為聊天室 ID 或使用者 ID。</param>
/// <param name="DisplayName">目標的顯示名稱，可為 <see langword="null"/>（若平台未提供）。</param>
/// <param name="UpdatedAt">此筆最近目標資訊的最後更新時間戳記（含時區資訊）。</param>
public sealed record RecentTargetInfo(
    string Channel,
    string TargetId,
    string? DisplayName,
    DateTimeOffset UpdatedAt);
