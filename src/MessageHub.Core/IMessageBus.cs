namespace MessageHub.Core;

/// <summary>
/// 訊息匯流排介面 — 提供 Outbound 緩衝佇列的生產者/消費者模型。
/// 對應 MESSAGE_BUS_ARCHITECTURE 規格文件中的 IMessageBus。
/// </summary>
public interface IMessageBus
{
    ValueTask PublishOutboundAsync(OutboundMessage message, CancellationToken cancellationToken = default);
    IAsyncEnumerable<OutboundMessage> ConsumeOutboundAsync(CancellationToken cancellationToken);
}
