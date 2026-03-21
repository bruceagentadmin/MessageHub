using MessageHub.Domain;

namespace MessageHub.Application;

public sealed class MessageOrchestrator(IMessageLogStore logStore, IChannelRegistry channelRegistry) : IMessageOrchestrator
{
    public async Task<MessageLogEntry> HandleInboundAsync(string tenantId, string channel, WebhookTextMessageRequest request, CancellationToken cancellationToken = default)
    {
        var client = channelRegistry.Get(channel);
        var inbound = await client.ParseAsync(tenantId, request, cancellationToken);

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
        var outbound = new OutboundMessage(
            request.TenantId,
            request.Channel,
            request.TargetId,
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
