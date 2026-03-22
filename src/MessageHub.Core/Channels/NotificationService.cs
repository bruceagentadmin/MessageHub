namespace MessageHub.Core;

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
    public async Task SendNotificationAsync(string tenantId, string channelName, string message, CancellationToken cancellationToken = default)
    {
        var config = await channelSettingsService.GetAsync(cancellationToken);
        config.Channels.TryGetValue(channelName, out var settings);

        if (settings is not { Enabled: true })
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
            message);

        await messageBus.PublishOutboundAsync(outbound, cancellationToken);
    }
}
