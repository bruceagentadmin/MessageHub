namespace MessageHub.Core.Models;

/// <summary>
/// 入站訊息 — 代表從外部頻道接收到的原始訊息資料。
/// </summary>
/// <remarks>
/// 此記錄由各頻道的 Webhook 處理器建立，並傳入訊息處理管線進行後續處理與日誌記錄。
/// <see cref="OriginalPayload"/> 為選填欄位，可保留原始平台的 JSON 酬載以便稽核或除錯。
/// </remarks>
/// <param name="TenantId">租戶識別碼，用於區隔多租戶環境中的資料歸屬。</param>
/// <param name="Channel">來源頻道的識別字串，例如 <c>telegram</c>、<c>line</c>。</param>
/// <param name="ChatId">訊息所在的聊天室或對話識別碼，用於後續回覆時定位目標。</param>
/// <param name="SenderId">發送者的平台識別碼。</param>
/// <param name="Content">訊息的純文字內容。</param>
/// <param name="ReceivedAt">訊息接收的時間戳記（含時區資訊）。</param>
/// <param name="OriginalPayload">來自外部平台的原始 JSON 酬載字串，可為 <see langword="null"/>。</param>
public sealed record InboundMessage(
    string TenantId,
    string Channel,
    string ChatId,
    string SenderId,
    string Content,
    DateTimeOffset ReceivedAt,
    string? OriginalPayload = null);
