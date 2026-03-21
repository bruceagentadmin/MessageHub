using System.Threading.Channels;
using MessageHub.Core;

namespace MessageHub.Infrastructure;

/// <summary>
/// 訊息匯流排實作 — 使用 System.Threading.Channels 提供高性能的生產者/消費者模型。
/// 對應 MESSAGE_BUS_ARCHITECTURE 規格文件中的 MessageBus。
/// </summary>
public sealed class MessageBus : IMessageBus
{
    private readonly Channel<OutboundMessage> _outbound = Channel.CreateUnbounded<OutboundMessage>(
        new UnboundedChannelOptions { SingleReader = false, SingleWriter = false });

    public ValueTask PublishOutboundAsync(OutboundMessage message, CancellationToken cancellationToken = default)
        => _outbound.Writer.WriteAsync(message, cancellationToken);

    public async IAsyncEnumerable<OutboundMessage> ConsumeOutboundAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var message in _outbound.Reader.ReadAllAsync(cancellationToken))
        {
            yield return message;
        }
    }

    /// <summary>目前佇列中的訊息數量 (用於監控)</summary>
    public int PendingCount => _outbound.Reader.Count;
}
