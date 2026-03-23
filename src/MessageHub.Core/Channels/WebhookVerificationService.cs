using System.Net.Http.Json;
using MessageHub.Core.Models;

namespace MessageHub.Core.Channels;

/// <summary>
/// Webhook 驗證服務實作 — 負責驗證各頻道的 Webhook URL 是否正確設定並可連線。
/// 目前支援 LINE 與 Telegram 兩種驗證策略：
/// <list type="bullet">
///   <item><description>LINE：向設定的 WebhookUrl 發送空事件 POST 請求，驗證伺服器回應。</description></item>
///   <item><description>Telegram：呼叫 setWebhook API 將 Bot 綁定至設定的 WebhookUrl。</description></item>
/// </list>
/// </summary>
/// <param name="channelSettingsService">頻道設定服務，用於讀取各頻道的 WebhookUrl 與認證參數。</param>
public sealed class WebhookVerificationService(IChannelSettingsService channelSettingsService) : IWebhookVerificationService
{
    // 建立單一 HttpClient 實例供本服務內所有驗證請求重複使用，避免 Socket 耗盡問題
    private readonly HttpClient _httpClient = new();

    /// <summary>
    /// 驗證指定頻道的 Webhook 設定是否正確，並嘗試與遠端端點建立連線。
    /// </summary>
    /// <param name="channelId">要驗證的頻道識別碼（例如 "line"、"telegram"），不區分大小寫。</param>
    /// <param name="cancellationToken">用於取消非同步操作的取消權杖。</param>
    /// <returns>
    /// 包含驗證結果的 <see cref="WebhookVerifyResult"/>，內含頻道資訊、操作類型、HTTP 狀態碼與說明訊息。
    /// </returns>
    public async Task<WebhookVerifyResult> VerifyAsync(string channelId, CancellationToken cancellationToken = default)
    {
        // 讀取所有頻道設定，嘗試取得指定頻道的設定
        var config = await channelSettingsService.GetAsync(cancellationToken);
        config.Channels.TryGetValue(channelId, out var channel);

        // 找不到頻道設定時，立即回傳失敗結果，避免後續 null 存取
        if (channel is null)
        {
            return new WebhookVerifyResult(channelId, "Unknown", false, "none", null, "找不到指定頻道設定。");
        }

        // channelType 與 channelId 相同，用於後續的頻道類型判斷
        var channelType = channelId;

        // 從 Parameters 字典取出 WebhookUrl 並去除前後空白
        var webhookUrl = channel.Parameters.GetValueOrDefault("WebhookUrl")?.Trim();
        if (string.IsNullOrWhiteSpace(webhookUrl))
        {
            // 沒有設定 WebhookUrl 時，無法執行任何驗證，直接回傳失敗
            return new WebhookVerifyResult(channelId, channelType, false, "none", null, "WebhookUrl 未設定。", null);
        }

        // LINE 驗證策略：向設定的 WebhookUrl 模擬發送一個空 events 陣列的 POST 請求
        // LINE 官方建議此方式確認端點可正常接收 Webhook 事件
        if (channelType.Equals("Line", StringComparison.OrdinalIgnoreCase))
        {
            using var response = await _httpClient.PostAsJsonAsync(webhookUrl, new { destination = "verify", events = Array.Empty<object>() }, cancellationToken);
            return new WebhookVerifyResult(
                channelId,
                channelType,
                response.IsSuccessStatusCode,
                "verify",
                (int)response.StatusCode,
                response.IsSuccessStatusCode ? "LINE webhook 驗證成功（收到 HTTP 200 範圍回應）。" : "LINE webhook 驗證失敗。",
                webhookUrl);
        }

        // Telegram 驗證策略：呼叫 setWebhook API 將 Bot 綁定至指定 URL
        // 此操作同時驗證 BotToken 有效性與 WebhookUrl 可連線性
        if (channelType.Equals("Telegram", StringComparison.OrdinalIgnoreCase))
        {
            // 確認 BotToken 已設定，缺少時無法呼叫 Telegram API
            var botToken = channel.Parameters.GetValueOrDefault("BotToken")?.Trim();
            if (string.IsNullOrWhiteSpace(botToken))
            {
                return new WebhookVerifyResult(channelId, channelType, false, "bind", null, "BotToken 未設定。", webhookUrl);
            }

            // 使用 GET 方式呼叫 setWebhook API（Telegram API 同時支援 GET 與 POST）
            // WebhookUrl 需要 URL 編碼以確保特殊字元正確傳遞
            var apiUrl = $"https://api.telegram.org/bot{botToken}/setWebhook?url={Uri.EscapeDataString(webhookUrl)}";
            using var response = await _httpClient.GetAsync(apiUrl, cancellationToken);

            // 讀取回應 Body，包含 Telegram API 回傳的詳細訊息（例如 "Webhook was set" 或錯誤描述）
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            return new WebhookVerifyResult(
                channelId,
                channelType,
                response.IsSuccessStatusCode,
                "bind",
                (int)response.StatusCode,
                response.IsSuccessStatusCode ? "Telegram webhook 綁定成功。" : "Telegram webhook 綁定失敗。",
                webhookUrl,
                body);
        }

        // 不在支援清單中的頻道類型，回傳明確的不支援說明
        return new WebhookVerifyResult(channelId, channelType, false, "none", null, "目前只支援 Line / Telegram 驗證。", webhookUrl);
    }
}
