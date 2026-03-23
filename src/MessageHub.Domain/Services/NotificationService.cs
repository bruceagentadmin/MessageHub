using MessageHub.Core;
using MessageHub.Core.Models;

namespace MessageHub.Domain.Services;

/// <summary>
/// 主動通知服務實作 — 結合 ChannelFactory 與 IChannelSettingsService。
/// 負責讀取設定中的 <c>NotificationTargetId</c>，並將訊息排入 MessageBus 的 Outbound 佇列，
/// 由背景的 ChannelManager 負責實際分發。
/// </summary>
internal sealed class NotificationService(
    ChannelFactory channelFactory,
    IChannelSettingsService channelSettingsService,
    IMessageBus messageBus) : INotificationService
{
    /// <inheritdoc />
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

        _ = channelFactory.GetChannel(channelName);

        var outbound = new OutboundMessage(
            tenantId,
            channelName,
            targetId,
            message);

        await messageBus.PublishOutboundAsync(outbound, cancellationToken);
    }
}
