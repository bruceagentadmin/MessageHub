namespace MessageHub.Core.Models;

/// <summary>
/// 發送訊息請求 — 控制中心手動觸發訊息發送的請求酬載。
/// </summary>
/// <remarks>
/// 對應 <c>POST /api/control/send</c> 端點。
/// 若 <see cref="TargetId"/> 為空字串，系統會自動從 <c>RecentTargetStore</c> 查找
/// 該頻道最近互動的目標對象（參見 <see cref="RecentTargetInfo"/>）。
/// 建立的 <see cref="OutboundMessage"/> 將排入訊息匯流排佇列，由背景服務非同步發送。
/// </remarks>
/// <param name="TenantId">租戶識別碼，用於區隔多租戶環境中的資料歸屬。</param>
/// <param name="Channel">目標頻道的識別字串，例如 <c>telegram</c>、<c>line</c>。</param>
/// <param name="TargetId">目標聊天室或使用者的識別碼；留空時系統自動填入最近互動目標。</param>
/// <param name="Content">欲發送的訊息純文字內容。</param>
/// <param name="TriggeredBy">觸發此發送請求的來源描述，例如 <c>Postman</c>、<c>UI</c>，可為 <see langword="null"/>。</param>
public sealed record SendMessageRequest(
    string TenantId,
    string Channel,
    string TargetId,
    string Content,
    string? TriggeredBy);
