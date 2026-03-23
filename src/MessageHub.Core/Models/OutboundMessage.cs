namespace MessageHub.Core.Models;

/// <summary>
/// 出站訊息 — 代表系統欲透過特定頻道發送給目標的訊息資料。
/// </summary>
/// <remarks>
/// 此記錄由控制中心或自動回覆邏輯建立，並排入 <c>IMessageBus</c> 佇列，
/// 由背景的 <c>ChannelManager</c> 負責實際呼叫對應頻道的 API 進行發送。
/// 若發送失敗（含重試耗盡），訊息將封裝為 <see cref="DeadLetterMessage"/> 進入死信佇列。
/// </remarks>
/// <param name="TenantId">租戶識別碼，用於區隔多租戶環境中的資料歸屬。</param>
/// <param name="Channel">目標頻道的識別字串，例如 <c>telegram</c>、<c>line</c>。</param>
/// <param name="ChatId">目標聊天室或對話的識別碼，決定訊息的最終收件人。</param>
/// <param name="Content">訊息的純文字內容。</param>
/// <param name="Metadata">附加的非結構化中繼資料，可為 <see langword="null"/>；各頻道可自行解析使用。</param>
public sealed record OutboundMessage(
    string TenantId,
    string Channel,
    string ChatId,
    string Content,
    object? Metadata = null);
