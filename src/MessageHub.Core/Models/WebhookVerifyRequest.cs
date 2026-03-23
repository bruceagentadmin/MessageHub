namespace MessageHub.Core.Models;

/// <summary>
/// Webhook 驗證請求 — 用於向後端發起驗證特定頻道 Webhook 連線狀態的請求。
/// </summary>
/// <remarks>
/// 對應 <c>POST /api/control/channel-settings/verify-webhook</c> 端點。
/// 後端收到此請求後，會嘗試呼叫對應頻道的驗證機制，並回傳 <see cref="WebhookVerifyResult"/>。
/// </remarks>
/// <param name="ChannelId">欲驗證 Webhook 的頻道識別碼，例如 <c>telegram</c>、<c>line</c>。</param>
public sealed record WebhookVerifyRequest(string ChannelId);
