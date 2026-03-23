namespace MessageHub.Core.Models;

/// <summary>
/// Webhook 文字訊息請求 — 通用 Webhook 端點接收文字訊息的請求酬載。
/// </summary>
/// <remarks>
/// 用於 <c>POST /api/webhooks/{channel}/{tenantId}/text</c> 端點，
/// 不需要實際的頻道 Token，適合本地整合測試。
/// 此請求由控制器解析後，建構為 <see cref="InboundMessage"/> 傳入處理管線。
/// </remarks>
/// <param name="ChatId">目標聊天室或對話的識別碼。</param>
/// <param name="SenderId">訊息發送者的識別碼。</param>
/// <param name="Content">訊息的純文字內容。</param>
public sealed record WebhookTextMessageRequest(
    string ChatId,
    string SenderId,
    string Content);
