using MessageHub.Core.Models;

namespace MessageHub.Core.Channels;

/// <summary>
/// 主動通知服務實作 — 結合 ChannelFactory 與 IChannelSettingsService。
/// 對應規格文件中的 NotificationService。
/// 負責讀取設定中的 <c>NotificationTargetId</c>，並將訊息排入 MessageBus 的 Outbound 佇列，
/// 由背景的 <see cref="MessageHub.Core.Bus.ChannelManager"/> 負責實際分發。
/// </summary>
/// <param name="channelFactory">頻道工廠，用於驗證指定頻道是否存在於系統中。</param>
/// <param name="channelSettingsService">頻道設定服務，用於讀取頻道啟用狀態與 NotificationTargetId。</param>
/// <param name="messageBus">訊息匯流排，用於將出站訊息排入 Outbound 佇列。</param>
internal sealed class NotificationService(
    ChannelFactory channelFactory,
    IChannelSettingsService channelSettingsService,
    IMessageBus messageBus) : INotificationService
{
    /// <summary>
    /// 從配置中尋找 NotificationTargetId 並透過 MessageBus 發送訊息。
    /// </summary>
    /// <param name="tenantId">租戶識別碼，用於多租戶隔離。</param>
    /// <param name="channelName">要發送通知的目標頻道名稱（例如 "telegram"、"line"）。</param>
    /// <param name="message">要發送的通知訊息文字內容。</param>
    /// <param name="cancellationToken">用於取消非同步操作的取消權杖。</param>
    /// <exception cref="InvalidOperationException">
    /// 當指定頻道未啟用或不存在時擲回；
    /// 或當頻道設定中未設定 <c>NotificationTargetId</c> 時擲回。
    /// </exception>
    public async Task SendNotificationAsync(string tenantId, string channelName, string message, CancellationToken cancellationToken = default)
    {
        // 讀取所有頻道設定，並嘗試取得指定頻道的設定
        var config = await channelSettingsService.GetAsync(cancellationToken);
        config.Channels.TryGetValue(channelName, out var settings);

        // 確認頻道存在且已啟用；未啟用的頻道不應接收主動通知
        if (settings is not { Enabled: true })
        {
            throw new InvalidOperationException($"頻道 {channelName} 未啟用或不存在");
        }

        // 從設定中讀取 NotificationTargetId（預設的通知接收對象，例如管理員的 Chat ID）
        var targetId = settings.Parameters.GetValueOrDefault("NotificationTargetId")?.Trim();
        if (string.IsNullOrWhiteSpace(targetId))
        {
            // 沒有目標 ID 時，無法得知要發給誰，必須拋出例外
            throw new InvalidOperationException($"頻道 {channelName} 未設定 NotificationTargetId");
        }

        // 確認頻道存在於 ChannelFactory 中（確保對應的 IChannel 實作有被註冊）
        // 丟棄回傳值是刻意的：此處只做驗證，不需要持有頻道實例
        _ = channelFactory.GetChannel(channelName);

        // 建立出站訊息並排入 MessageBus Outbound 佇列
        // 由背景服務 ChannelManager 負責實際呼叫頻道 API 發送
        var outbound = new OutboundMessage(
            tenantId,
            channelName,
            targetId,
            message);

        await messageBus.PublishOutboundAsync(outbound, cancellationToken);
    }
}
