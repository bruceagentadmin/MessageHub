using MessageHub.Core;

namespace MessageHub.Infrastructure;

/// <summary>
/// 主動通知服務實作 — 結合 ChannelFactory 與 IChannelSettingsService。
/// 對應規格文件中的 NotificationService。
/// </summary>
public sealed class NotificationService(
    ChannelFactory channelFactory,
    IChannelSettingsService channelSettingsService,
    IMessageBus messageBus) : INotificationService
{
    /// <summary>
    /// 從配置中尋找 NotificationTargetId 並透過 MessageBus 發送訊息。
    /// </summary>
    public async Task SendGlobalNotificationAsync(string tenantId, string channelName, string message, CancellationToken cancellationToken = default)
    {
        var config = await channelSettingsService.GetAsync(cancellationToken);
        var settings = config.Channels.FirstOrDefault(
            x => x.Enabled && x.Type.Equals(channelName, StringComparison.OrdinalIgnoreCase));

        if (settings is null)
        {
            throw new InvalidOperationException($"頻道 {channelName} 未啟用或不存在");
        }

        var targetId = settings.Parameters.GetValueOrDefault("NotificationTargetId")?.Trim();
        if (string.IsNullOrWhiteSpace(targetId))
        {
            throw new InvalidOperationException($"頻道 {channelName} 未設定 NotificationTargetId");
        }

        // 確認頻道存在
        _ = channelFactory.GetChannel(channelName);

        var outbound = new OutboundMessage(
            tenantId,
            channelName,
            targetId,
            message,
            DateTimeOffset.UtcNow,
            "NotificationService");

        await messageBus.PublishOutboundAsync(outbound, cancellationToken);
    }
}
