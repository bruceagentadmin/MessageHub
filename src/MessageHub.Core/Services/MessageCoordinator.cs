using MessageHub.Core.Models;

namespace MessageHub.Core.Services;

/// <summary>
/// 訊息協調器 — 實作 <see cref="IMessageCoordinator"/>，負責 Webhook 進站處理、手動發送、日誌查詢與頻道清單。
/// <para>
/// 本類別將原 <c>UnifiedMessageProcessor</c> 中的協調/調度職責獨立出來，
/// 透過 <see cref="IMessageBus"/> 推送訊息至佇列（發後即忘），不直接呼叫 <see cref="IChannel.SendAsync"/>。
/// </para>
/// </summary>
public sealed class MessageCoordinator(
    IMessageLogStore logStore,
    ChannelFactory channelFactory,
    IRecentTargetStore recentTargetStore,
    IMessageBus messageBus,
    IMessageProcessor messageProcessor) : IMessageCoordinator
{
    /// <inheritdoc />
    public async Task<MessageLogEntry> HandleInboundAsync(string tenantId, string channel, WebhookTextMessageRequest request, CancellationToken cancellationToken = default)
    {
        // 1. 透過對應頻道解析 Webhook 請求為統一的 InboundMessage
        var client = channelFactory.GetChannel(channel);
        var inbound = await client.ParseRequestAsync(tenantId, request, cancellationToken);

        // 2. 記錄最近互動對象，供後續手動發送時自動填入 targetId
        await recentTargetStore.SetLastTargetAsync(inbound.Channel, inbound.ChatId, inbound.SenderId, cancellationToken);

        // 3. 記錄 Inbound 日誌
        var inboundLog = new MessageLogEntry(
            Guid.NewGuid(),
            inbound.ReceivedAt,
            inbound.TenantId,
            inbound.Channel,
            MessageDirection.Inbound,
            DeliveryStatus.Delivered,
            inbound.ChatId,
            inbound.SenderId,
            inbound.Content,
            $"{inbound.Channel} webhook",
            $"Sender={inbound.SenderId}");

        await logStore.AddAsync(inboundLog, cancellationToken);

        // 4. 透過 IMessageProcessor 產生自動回覆文字
        var replyText = await messageProcessor.ProcessAsync(inbound, cancellationToken);

        // 5. 組裝 OutboundMessage 並推送至 MessageBus（發後即忘）
        var reply = new OutboundMessage(
            inbound.TenantId,
            inbound.Channel,
            inbound.ChatId,
            replyText,
            new { CreatedAt = DateTimeOffset.UtcNow, TriggeredBy = "AutoReply", TargetDisplayName = inbound.SenderId });

        await messageBus.PublishOutboundAsync(reply, cancellationToken);

        return inboundLog;
    }

    /// <inheritdoc />
    public async Task<MessageLogEntry> SendManualAsync(SendMessageRequest request, CancellationToken cancellationToken = default)
    {
        // 確認頻道存在（若不存在會拋出 KeyNotFoundException）
        _ = channelFactory.GetChannel(request.Channel);
        var targetId = request.TargetId;
        RecentTargetInfo? recent = null;

        // 嘗試取得該頻道最近的互動對象
        recent = await recentTargetStore.GetLastTargetAsync(request.Channel, cancellationToken);

        // 若呼叫端未提供 targetId，使用最近互動對象作為 fallback
        if (string.IsNullOrWhiteSpace(targetId))
        {
            targetId = recent?.TargetId;
        }

        // 若仍無 targetId，記錄失敗日誌並回傳
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
                null,
                request.Content,
                "control center",
                "找不到可用的 targetId，且該頻道沒有最近互動對象");
            await logStore.AddAsync(failedLog, cancellationToken);
            return failedLog;
        }

        // 嘗試取得目標的顯示名稱（僅當 targetId 與最近互動對象一致時）
        var targetDisplayName = recent?.TargetId.Equals(targetId, StringComparison.OrdinalIgnoreCase) == true
            ? recent.DisplayName
            : null;

        // 組裝 OutboundMessage 並推送至 MessageBus
        var outbound = new OutboundMessage(
            request.TenantId,
            request.Channel,
            targetId,
            request.Content,
            new { CreatedAt = DateTimeOffset.UtcNow, TriggeredBy = request.TriggeredBy ?? "ControlCenter", TargetDisplayName = targetDisplayName });

        await messageBus.PublishOutboundAsync(outbound, cancellationToken);

        // 記錄 Pending 狀態的日誌（實際發送由 ChannelManager 背景處理）
        var pendingLog = new MessageLogEntry(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            request.TenantId,
            request.Channel,
            MessageDirection.Outbound,
            DeliveryStatus.Pending,
            targetId,
            targetDisplayName,
            request.Content,
            "control center",
            $"Queued by {request.TriggeredBy ?? "ControlCenter"}");

        await logStore.AddAsync(pendingLog, cancellationToken);
        return pendingLog;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<MessageLogEntry>> GetRecentLogsAsync(int count, CancellationToken cancellationToken = default)
        => logStore.GetRecentAsync(count, cancellationToken);

    /// <inheritdoc />
    public IReadOnlyList<ChannelDefinition> GetChannels()
        => channelFactory.GetDefinitions();
}
