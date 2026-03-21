using MessageHub.Domain;

namespace MessageHub.Application;

public sealed class MessageOrchestrator(
    IMessageLogStore logStore,
    IChannelRegistry channelRegistry,
    IRecentTargetStore recentTargetStore) : IMessageOrchestrator
{
    public async Task<MessageLogEntry> HandleInboundAsync(string tenantId, string channel, WebhookTextMessageRequest request, CancellationToken cancellationToken = default)
    {
        var client = channelRegistry.Get(channel);
        var inbound = await client.ParseAsync(tenantId, request, cancellationToken);

        await recentTargetStore.SetLastTargetAsync(inbound.Channel, inbound.ChatId, inbound.SenderId, cancellationToken);

        var inboundLog = new MessageLogEntry(
            Guid.NewGuid(),
            inbound.ReceivedAt,
            inbound.TenantId,
            inbound.Channel,
            MessageDirection.Inbound,
            DeliveryStatus.Delivered,
            inbound.ChatId,
            inbound.Content,
            $"{inbound.Channel} webhook",
            $"Sender={inbound.SenderId}");

        await logStore.AddAsync(inboundLog, cancellationToken);

        var reply = new OutboundMessage(
            inbound.TenantId,
            inbound.Channel,
            inbound.ChatId,
            $"[POC 回覆] 已收到：{inbound.Content}",
            DateTimeOffset.UtcNow,
            "AutoReply");

        var replyLog = await client.SendAsync(reply, cancellationToken);
        await logStore.AddAsync(replyLog, cancellationToken);

        return replyLog;
    }

    public async Task<MessageLogEntry> SendManualAsync(SendMessageRequest request, CancellationToken cancellationToken = default)
    {
        var client = channelRegistry.Get(request.Channel);
        var targetId = request.TargetId;

        if (string.IsNullOrWhiteSpace(targetId))
        {
            var recent = await recentTargetStore.GetLastTargetAsync(request.Channel, cancellationToken);
            targetId = recent?.TargetId;
        }

        if (string.IsNullOrWhiteSpace(targetId))
        {
            var failedLog = new MessageLogEntry(
                Guid.NewGuid(),
                DateTimeOffset.UtcNow,
                request.TenantId,
                request.Channel,
                MessageDirection.Outbound,
                DeliveryStatus.Failed,
                "unknown",
                request.Content,
                "control center",
                "找不到可用的 targetId，且該頻道沒有最近互動對象");
            await logStore.AddAsync(failedLog, cancellationToken);
            return failedLog;
        }

        var outbound = new OutboundMessage(
            request.TenantId,
            request.Channel,
            targetId,
            request.Content,
            DateTimeOffset.UtcNow,
            request.TriggeredBy ?? "ControlCenter");

        var log = await client.SendAsync(outbound, cancellationToken);
        await logStore.AddAsync(log, cancellationToken);
        return log;
    }

    public Task<IReadOnlyList<MessageLogEntry>> GetRecentLogsAsync(int count, CancellationToken cancellationToken = default)
        => logStore.GetRecentAsync(count, cancellationToken);

    public IReadOnlyList<ChannelDefinition> GetChannels()
        => channelRegistry.GetDefinitions();
}
