using MessageHub.Core.Models;

namespace MessageHub.Core.Channels;

/// <summary>
/// Email 頻道實作 — 直接實作 IChannel。
/// Email 模擬通道，先以文字送達紀錄驗證流程；SMTP 實際發送功能保留為未來擴充點。
/// 目前 <see cref="SendAsync"/> 為空操作（no-op），代表本 POC 階段 Email 發送尚未實作。
/// </summary>
/// <param name="channelSettingsService">頻道設定服務，保留供未來擴充 SMTP 設定讀取使用。</param>
internal sealed class EmailChannel(IChannelSettingsService channelSettingsService) : IChannel
{
    // 保留對設定服務的參考，供未來實作 SMTP 設定讀取（SmtpHost、Port、Credentials 等）時使用
    private readonly IChannelSettingsService _channelSettingsService = channelSettingsService;

    /// <inheritdoc />
    public string Name => "email";

    /// <summary>
    /// 將 Webhook 接收到的原始文字訊息請求，解析為系統內部的 <see cref="InboundMessage"/> 格式。
    /// </summary>
    /// <param name="tenantId">租戶識別碼，用於多租戶隔離。</param>
    /// <param name="request">Webhook 傳入的文字訊息請求，包含寄件者、收件人與訊息內容。</param>
    /// <param name="cancellationToken">用於取消非同步操作的取消權杖。</param>
    /// <returns>轉換後的 <see cref="InboundMessage"/>，時間戳記使用 UTC 時間。</returns>
    public Task<InboundMessage> ParseRequestAsync(string tenantId, WebhookTextMessageRequest request, CancellationToken cancellationToken = default)
    {
        // 將 Email Webhook 的 WebhookTextMessageRequest 對映為系統統一的 InboundMessage 格式
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
    /// （模擬）透過 Email 發送訊息至指定收件人。
    /// </summary>
    /// <remarks>
    /// 目前為空操作（no-op），僅完成 Task 而不執行實際 SMTP 發送。
    /// 未來應整合 MailKit 或 SmtpClient，讀取 SMTP 設定並實際寄出郵件。
    /// </remarks>
    /// <param name="chatId">目標收件人的 Email 地址。</param>
    /// <param name="message">要發送的出站訊息，包含郵件內容。</param>
    /// <param name="settings">可選的頻道設定（含 SmtpHost 等），目前未使用。</param>
    /// <param name="cancellationToken">用於取消非同步操作的取消權杖。</param>
    /// <returns>已完成的 <see cref="Task"/>（no-op）。</returns>
    public Task SendAsync(string chatId, OutboundMessage message, ChannelSettings? settings = null, CancellationToken cancellationToken = default)
        // POC 階段：Email 發送為空操作，僅讓整體流程可以跑通而不拋出例外
        => Task.CompletedTask;
}
