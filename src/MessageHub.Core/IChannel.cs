namespace MessageHub.Core;

/// <summary>
/// 頻道通用介面 — 定義所有通訊頻道的基礎行為。
/// 對應規格文件中的 IChannel。
/// </summary>
public interface IChannel
{
    /// <summary>頻道識別名稱 (如 line, telegram)</summary>
    string Name { get; }

    /// <summary>解析來自平台的 Webhook 請求並轉換為 InboundMessage</summary>
    Task<InboundMessage> ParseRequestAsync(string tenantId, WebhookTextMessageRequest request, CancellationToken cancellationToken = default);

    /// <summary>發送 OutboundMessage 到指定的 ChatId</summary>
    Task<MessageLogEntry> SendAsync(string chatId, OutboundMessage message, ChannelSettings? settings = null, CancellationToken cancellationToken = default);
}
