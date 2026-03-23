namespace MessageHub.Core.Models;

/// <summary>
/// Webhook 驗證結果 — 封裝對特定頻道執行 Webhook 驗證操作後的完整回應資訊。
/// </summary>
/// <remarks>
/// 由 <c>WebhookVerificationService</c> 產生並回傳給控制中心 API，
/// 前端可依據 <see cref="Success"/> 與 <see cref="StatusCode"/> 呈現驗證狀態與錯誤詳情。
/// </remarks>
/// <param name="ChannelId">被驗證頻道的識別碼，例如 <c>telegram</c>、<c>line</c>。</param>
/// <param name="ChannelType">頻道的類型名稱（通常與 <paramref name="ChannelId"/> 相同）。</param>
/// <param name="Success">驗證是否成功；<see langword="true"/> 表示 Webhook 連線正常。</param>
/// <param name="Action">本次驗證所執行的動作描述，例如 <c>SetWebhook</c>、<c>GetWebhookInfo</c>。</param>
/// <param name="StatusCode">外部平台回傳的 HTTP 狀態碼，可為 <see langword="null"/>（若未發出 HTTP 請求）。</param>
/// <param name="Message">驗證結果的人類可讀訊息，包含成功說明或錯誤原因。</param>
/// <param name="WebhookUrl">本次驗證所使用或設定的 Webhook URL，可為 <see langword="null"/>。</param>
/// <param name="Details">附加的詳細資訊或堆疊追蹤，可為 <see langword="null"/>，通常於失敗時填入。</param>
public sealed record WebhookVerifyResult(
    string ChannelId,
    string ChannelType,
    bool Success,
    string Action,
    int? StatusCode,
    string Message,
    string? WebhookUrl = null,
    string? Details = null);
