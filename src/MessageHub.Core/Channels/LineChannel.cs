using System.Net.Http.Headers;
using System.Net.Http.Json;
using MessageHub.Core.Models;

namespace MessageHub.Core.Channels;

/// <summary>
/// LINE 頻道實作 — 直接實作 IChannel。
/// 專門處理 LINE Messaging API 的邏輯，包含 Signature 驗證、LineEvent 解析與 API 呼叫。
/// 使用 Channel Access Token 作為 Bearer Token 呼叫 LINE Push Message API。
/// </summary>
/// <param name="channelSettingsService">頻道設定服務，用於讀取 ChannelAccessToken 等動態設定。</param>
/// <param name="httpClient">可注入的 HTTP 客戶端，預設為新建實例（測試時可替換為假實作）。</param>
public sealed class LineChannel(IChannelSettingsService channelSettingsService, HttpClient? httpClient = null) : IChannel
{
    private readonly IChannelSettingsService _channelSettingsService = channelSettingsService;

    // 若外部未注入 HttpClient，則建立預設實例；測試時可注入 Mock 以避免實際 HTTP 呼叫
    private readonly HttpClient _httpClient = httpClient ?? new HttpClient();

    /// <inheritdoc />
    public string Name => "line";

    /// <summary>
    /// 將 Webhook 接收到的原始文字訊息請求，解析為系統內部的 <see cref="InboundMessage"/> 格式。
    /// </summary>
    /// <param name="tenantId">租戶識別碼，用於多租戶隔離。</param>
    /// <param name="request">Webhook 傳入的文字訊息請求，包含 ChatId（replyToken 或 userId）、SenderId 與訊息內容。</param>
    /// <param name="cancellationToken">用於取消非同步操作的取消權杖。</param>
    /// <returns>轉換後的 <see cref="InboundMessage"/>，時間戳記使用 UTC 時間。</returns>
    public Task<InboundMessage> ParseRequestAsync(string tenantId, WebhookTextMessageRequest request, CancellationToken cancellationToken = default)
    {
        // 將 LINE Webhook 的 WebhookTextMessageRequest 對映為系統統一的 InboundMessage 格式
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
    /// 透過 LINE Push Message API 將訊息主動發送至指定使用者或群組。
    /// </summary>
    /// <param name="chatId">目標 LINE 使用者 ID 或群組 ID（即 Push API 的 <c>to</c> 欄位）。</param>
    /// <param name="message">要發送的出站訊息，包含訊息內容。</param>
    /// <param name="settings">
    /// 可選的頻道設定（含 ChannelAccessToken）。若未提供，則自動從 <see cref="IChannelSettingsService"/> 讀取。
    /// 預先傳入可避免重複讀取設定，提升批次發送效能。
    /// </param>
    /// <param name="cancellationToken">用於取消非同步操作的取消權杖。</param>
    /// <exception cref="InvalidOperationException">當 ChannelAccessToken 未設定或設定為空白時擲回。</exception>
    /// <exception cref="HttpRequestException">當 LINE API 回應非成功狀態碼時擲回。</exception>
    public async Task SendAsync(string chatId, OutboundMessage message, ChannelSettings? settings = null, CancellationToken cancellationToken = default)
    {
        // 若呼叫方未傳入 settings，從設定服務讀取；
        // 這樣設計讓 ChannelManager 可以預先載入設定並重複使用，減少 I/O 次數
        settings ??= await GetSettingsAsync(cancellationToken);

        // 從設定的 Parameters 字典中取出 ChannelAccessToken，並去除前後空白
        var token = settings?.Parameters.GetValueOrDefault("ChannelAccessToken")?.Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            // ChannelAccessToken 是 LINE API 的身份憑證，缺少時無法發送訊息，直接拋出例外
            throw new InvalidOperationException("ChannelAccessToken 未設定");
        }

        // 建立 HTTP POST 請求並設置 Authorization Bearer Token（LINE API 規格要求）
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.line.me/v2/bot/message/push");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // 建立符合 LINE Push Message API 規格的 JSON Payload：
        // to: 目標 ID；messages: 訊息陣列（此處只傳送一則文字訊息）
        request.Content = JsonContent.Create(new
        {
            to = chatId,
            messages = new[] { new { type = "text", text = message.Content } }
        });

        // 發送請求並確認回應狀態碼，非 2xx 時自動拋出 HttpRequestException
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// 從設定服務讀取 LINE 頻道設定，並確認頻道已啟用。
    /// </summary>
    /// <param name="cancellationToken">用於取消非同步操作的取消權杖。</param>
    /// <returns>
    /// 已啟用的 <see cref="ChannelSettings"/>；若頻道不存在或未啟用，則回傳 <c>null</c>。
    /// </returns>
    private async Task<ChannelSettings?> GetSettingsAsync(CancellationToken cancellationToken)
    {
        var config = await _channelSettingsService.GetAsync(cancellationToken);

        // 透過 ChannelSettingsResolver 以不區分大小寫的方式尋找 line 設定
        var settings = ChannelSettingsResolver.FindSettings(config, "line");

        // 只有在設定存在且 Enabled = true 時才回傳，確保停用的頻道不會被誤用
        return settings is { Enabled: true } ? settings : null;
    }
}
