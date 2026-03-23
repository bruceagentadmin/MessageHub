using MessageHub.Core.Models;

namespace MessageHub.Core;

/// <summary>
/// 頻道通用介面 — 定義所有通訊頻道（如 Line、Telegram、Email）的基礎行為契約。
/// 對應規格文件中的 IChannel。
/// </summary>
public interface IChannel
{
    /// <summary>
    /// 取得頻道的識別名稱（如 <c>line</c>、<c>telegram</c>、<c>email</c>）。
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 非同步解析來自平台的 Webhook 文字請求，並轉換為統一的
    /// <see cref="InboundMessage"/> 格式。
    /// </summary>
    /// <param name="tenantId">觸發此 Webhook 的租戶識別碼。</param>
    /// <param name="request">包含原始平台資料的 <see cref="WebhookTextMessageRequest"/> 請求物件。</param>
    /// <param name="cancellationToken">用於取消非同步操作的權杖。</param>
    /// <returns>轉換後的 <see cref="InboundMessage"/> 入站訊息物件。</returns>
    Task<InboundMessage> ParseRequestAsync(string tenantId, WebhookTextMessageRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 非同步將外送訊息傳送至指定的聊天目標。
    /// </summary>
    /// <param name="chatId">目標聊天室或使用者的識別碼（平台原生格式）。</param>
    /// <param name="message">要發送的 <see cref="OutboundMessage"/> 外送訊息物件。</param>
    /// <param name="settings">
    /// 可選的 <see cref="ChannelSettings"/> 頻道設定，包含 Token 等憑證資訊；
    /// 若為 <see langword="null"/>，則使用預設設定。
    /// </param>
    /// <param name="cancellationToken">用於取消非同步操作的權杖。</param>
    Task SendAsync(string chatId, OutboundMessage message, ChannelSettings? settings = null, CancellationToken cancellationToken = default);
}
