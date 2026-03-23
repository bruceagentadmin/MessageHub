using System.Net.Http.Json;
using MessageHub.Core.Models;

namespace MessageHub.Core.Channels;

/// <summary>
/// Telegram 頻道實作 — 直接實作 IChannel。
/// 專門處理 Telegram Bot API 的邏輯，包含 Update 解析與 Bot Client 發送訊息。
/// 使用 Bot Token 作為身份憑證，透過 sendMessage API 端點發送文字訊息。
/// </summary>
/// <param name="channelSettingsService">頻道設定服務，用於讀取 BotToken 等動態設定。</param>
/// <param name="httpClient">可注入的 HTTP 客戶端，預設為新建實例（測試時可替換為假實作）。</param>
public sealed class TelegramChannel(IChannelSettingsService channelSettingsService, HttpClient? httpClient = null) : IChannel
{
    private readonly IChannelSettingsService _channelSettingsService = channelSettingsService;

    // 若外部未注入 HttpClient，則建立預設實例；測試時可注入 Mock 以避免實際 HTTP 呼叫
    private readonly HttpClient _httpClient = httpClient ?? new HttpClient();

    /// <inheritdoc />
    public string Name => "telegram";

    /// <summary>
    /// 將 Webhook 接收到的原始文字訊息請求，解析為系統內部的 <see cref="InboundMessage"/> 格式。
    /// </summary>
    /// <param name="tenantId">租戶識別碼，用於多租戶隔離。</param>
    /// <param name="request">Webhook 傳入的文字訊息請求，包含 ChatId、SenderId 與訊息內容。</param>
    /// <param name="cancellationToken">用於取消非同步操作的取消權杖。</param>
    /// <returns>轉換後的 <see cref="InboundMessage"/>，時間戳記使用 UTC 時間。</returns>
    public Task<InboundMessage> ParseRequestAsync(string tenantId, WebhookTextMessageRequest request, CancellationToken cancellationToken = default)
    {
        // 將 Telegram 的 WebhookTextMessageRequest 對映為系統統一的 InboundMessage 格式
        // OriginalPayload 保留原始 content 字串，供後續審計或除錯使用
        var inbound = new InboundMessage(
            tenantId,
            Name,
            request.ChatId,
            request.SenderId,
            request.Content,
            DateTimeOffset.UtcNow,
            OriginalPayload: request.Content);

        return Task.FromResult(inbound);
    }

    /// <summary>
    /// 透過 Telegram Bot API 將訊息發送至指定聊天室。
    /// </summary>
    /// <param name="chatId">目標 Telegram 聊天室的 Chat ID（可為個人或群組）。</param>
    /// <param name="message">要發送的出站訊息，包含訊息內容。</param>
    /// <param name="settings">
    /// 可選的頻道設定（含 BotToken）。若未提供，則自動從 <see cref="IChannelSettingsService"/> 讀取。
    /// 預先傳入可避免重複讀取設定，提升批次發送效能。
    /// </param>
    /// <param name="cancellationToken">用於取消非同步操作的取消權杖。</param>
    /// <exception cref="InvalidOperationException">當 BotToken 未設定或設定為空白時擲回。</exception>
    /// <exception cref="HttpRequestException">當 Telegram API 回應非成功狀態碼時擲回。</exception>
    public async Task SendAsync(string chatId, OutboundMessage message, ChannelSettings? settings = null, CancellationToken cancellationToken = default)
    {
        // 若呼叫方未傳入 settings，從設定服務讀取；
        // 這樣設計讓 ChannelManager 可以預先載入設定並重複使用，減少 I/O 次數
        settings ??= await GetSettingsAsync(cancellationToken);

        // 從設定的 Parameters 字典中取出 BotToken，並去除前後空白
        var botToken = settings?.Parameters.GetValueOrDefault("BotToken")?.Trim();
        if (string.IsNullOrWhiteSpace(botToken))
        {
            // BotToken 是 Telegram API 的身份憑證，缺少時無法發送訊息，直接拋出例外
            throw new InvalidOperationException("BotToken 未設定");
        }

        // 建立符合 Telegram sendMessage API 規格的 JSON Payload
        // chat_id: 目標聊天室識別碼；text: 訊息文字內容
        var payload = new { chat_id = chatId, text = message.Content };

        // 透過 HTTP POST 呼叫 Telegram Bot API，若回應狀態碼非成功則拋出 HttpRequestException
        using var response = await _httpClient.PostAsJsonAsync($"https://api.telegram.org/bot{botToken}/sendMessage", payload, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// 從設定服務讀取 Telegram 頻道設定，並確認頻道已啟用。
    /// </summary>
    /// <param name="cancellationToken">用於取消非同步操作的取消權杖。</param>
    /// <returns>
    /// 已啟用的 <see cref="ChannelSettings"/>；若頻道不存在或未啟用，則回傳 <c>null</c>。
    /// </returns>
    private async Task<ChannelSettings?> GetSettingsAsync(CancellationToken cancellationToken)
    {
        var config = await _channelSettingsService.GetAsync(cancellationToken);

        // 透過 ChannelSettingsResolver 以不區分大小寫的方式尋找 telegram 設定
        var settings = ChannelSettingsResolver.FindSettings(config, "telegram");

        // 只有在設定存在且 Enabled = true 時才回傳，確保停用的頻道不會被誤用
        return settings is { Enabled: true } ? settings : null;
    }
}
