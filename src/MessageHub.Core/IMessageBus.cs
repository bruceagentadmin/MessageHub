using MessageHub.Core.Models;

namespace MessageHub.Core;

/// <summary>
/// 訊息匯流排介面 — 提供 Inbound/Outbound/DLQ 緩衝佇列的生產者/消費者模型。
/// 對應 MESSAGE_BUS_ARCHITECTURE 規格文件中的 IMessageBus。
/// </summary>
public interface IMessageBus
{
    // Outbound
    ValueTask PublishOutboundAsync(OutboundMessage message, CancellationToken cancellationToken = default);
    IAsyncEnumerable<OutboundMessage> ConsumeOutboundAsync(CancellationToken cancellationToken);

    // Inbound
    ValueTask PublishInboundAsync(InboundMessage message, CancellationToken cancellationToken = default);
    IAsyncEnumerable<InboundMessage> ConsumeInboundAsync(CancellationToken cancellationToken);

    // Dead Letter Queue
    ValueTask PublishDeadLetterAsync(DeadLetterMessage message, CancellationToken cancellationToken = default);
    IAsyncEnumerable<DeadLetterMessage> ConsumeDeadLetterAsync(CancellationToken cancellationToken);
}
