using MessageHub.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MessageHub.Infrastructure;

/// <summary>
/// 渠道管理員 — 作為後台服務，負責監聽 Bus 上的訊息，並根據訊息指示分發到對應的實體渠道。
/// 對應 MESSAGE_BUS_ARCHITECTURE 規格文件中的 ChannelManager。
/// </summary>
public sealed class ChannelManager(
    IMessageBus messageBus,
    ChannelFactory channelFactory,
    IMessageLogStore logStore,
    ILogger<ChannelManager> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("ChannelManager 開始監聽 MessageBus...");

        await foreach (var message in messageBus.ConsumeOutboundAsync(stoppingToken))
        {
            try
            {
                var channel = channelFactory.GetChannel(message.Channel);
                var log = await channel.SendAsync(message.TargetId, message, null, stoppingToken);
                await logStore.AddAsync(log, stoppingToken);

                logger.LogInformation("訊息已發送至 {Channel} -> {TargetId}", message.Channel, message.TargetId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "發送訊息至 {Channel} 失敗", message.Channel);

                var failedLog = new MessageLogEntry(
                    Guid.NewGuid(),
                    DateTimeOffset.UtcNow,
                    message.TenantId,
                    message.Channel,
                    MessageDirection.Outbound,
                    DeliveryStatus.Failed,
                    message.TargetId,
                    message.Content,
                    "ChannelManager",
                    ex.Message);

                await logStore.AddAsync(failedLog, stoppingToken);
            }
        }
    }
}
