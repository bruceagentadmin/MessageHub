using MessageHub.Core;

namespace MessageHub.Application;

/// <summary>
/// 統一訊息處理核心 — 實現 IMessageProcessor，整合訊息處理邏輯。
/// 對應規格文件中的 UnifiedMessageProcessor。
/// 同時保留完整的協調功能 (HandleInbound, SendManual, GetLogs 等)。
/// </summary>
public sealed class UnifiedMessageProcessor(
    IMessageLogStore logStore,
    ChannelFactory channelFactory,
    IRecentTargetStore recentTargetStore,
    IMessageBus messageBus) : IMessageProcessor
{
    /// <summary>IMessageProcessor 介面實作 — 接收 InboundMessage 並回傳處理後的回覆文字</summary>
    public Task<string> ProcessAsync(InboundMessage message, CancellationToken cancellationToken = default)
    {
        return Task.FromResult($"[POC 回覆] 已收到：{message.Content}");
    }

    public async Task<MessageLogEntry> HandleInboundAsync(string tenantId, string channel, WebhookTextMessageRequest request, CancellationToken cancellationToken = default)
    {
        var client = channelFactory.GetChannel(channel);
        var inbound = await client.ParseRequestAsync(tenantId, request, cancellationToken);

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

        var replyText = await ProcessAsync(inbound, cancellationToken);

        var reply = new OutboundMessage(
            inbound.TenantId,
            inbound.Channel,
            inbound.ChatId,
            replyText);

        await messageBus.PublishOutboundAsync(reply, cancellationToken);

        return inboundLog;
    }

    public async Task<MessageLogEntry> SendManualAsync(SendMessageRequest request, CancellationToken cancellationToken = default)
    {
        var client = channelFactory.GetChannel(request.Channel);
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
            new { CreatedAt = DateTimeOffset.UtcNow, TriggeredBy = request.TriggeredBy ?? "ControlCenter" });

        await messageBus.PublishOutboundAsync(outbound, cancellationToken);

        var pendingLog = new MessageLogEntry(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            request.TenantId,
            request.Channel,
            MessageDirection.Outbound,
            DeliveryStatus.Pending,
            targetId,
            request.Content,
            "control center",
            $"Queued by {request.TriggeredBy ?? "ControlCenter"}");

        await logStore.AddAsync(pendingLog, cancellationToken);
        return pendingLog;
    }

    public Task<IReadOnlyList<MessageLogEntry>> GetRecentLogsAsync(int count, CancellationToken cancellationToken = default)
        => logStore.GetRecentAsync(count, cancellationToken);

    public IReadOnlyList<ChannelDefinition> GetChannels()
        => channelFactory.GetDefinitions();
}
