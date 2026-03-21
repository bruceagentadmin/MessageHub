using System.Threading.Channels;
using MessageHub.Core;

namespace MessageHub.Infrastructure;

/// <summary>
/// 訊息匯流排實作 — 使用 System.Threading.Channels 提供高性能的生產者/消費者模型。
/// 對應 MESSAGE_BUS_ARCHITECTURE 規格文件中的 MessageBus。
/// 提供 Outbound、Inbound、Dead Letter Queue 三條佇列。
/// </summary>
public sealed class MessageBus : IMessageBus
{
    private readonly Channel<OutboundMessage> _outbound = Channel.CreateUnbounded<OutboundMessage>(
        new UnboundedChannelOptions { SingleReader = false, SingleWriter = false });

    private readonly Channel<InboundMessage> _inbound = Channel.CreateUnbounded<InboundMessage>(
        new UnboundedChannelOptions { SingleReader = false, SingleWriter = false });

    private readonly Channel<DeadLetterMessage> _deadLetter = Channel.CreateUnbounded<DeadLetterMessage>(
        new UnboundedChannelOptions { SingleReader = false, SingleWriter = false });

    // Outbound
    public ValueTask PublishOutboundAsync(OutboundMessage message, CancellationToken cancellationToken = default)
        => _outbound.Writer.WriteAsync(message, cancellationToken);

    public async IAsyncEnumerable<OutboundMessage> ConsumeOutboundAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var message in _outbound.Reader.ReadAllAsync(cancellationToken))
            yield return message;
    }

    // Inbound
    public ValueTask PublishInboundAsync(InboundMessage message, CancellationToken cancellationToken = default)
        => _inbound.Writer.WriteAsync(message, cancellationToken);

    public async IAsyncEnumerable<InboundMessage> ConsumeInboundAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var message in _inbound.Reader.ReadAllAsync(cancellationToken))
            yield return message;
    }

    // Dead Letter Queue
    public ValueTask PublishDeadLetterAsync(DeadLetterMessage message, CancellationToken cancellationToken = default)
        => _deadLetter.Writer.WriteAsync(message, cancellationToken);

    public async IAsyncEnumerable<DeadLetterMessage> ConsumeDeadLetterAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var message in _deadLetter.Reader.ReadAllAsync(cancellationToken))
            yield return message;
    }

    /// <summary>目前 Outbound 佇列中的訊息數量 (用於監控)</summary>
    public int OutboundPendingCount => _outbound.Reader.Count;

    /// <summary>目前 Inbound 佇列中的訊息數量 (用於監控)</summary>
    public int InboundPendingCount => _inbound.Reader.Count;

    /// <summary>目前 DLQ 佇列中的訊息數量 (用於監控)</summary>
    public int DeadLetterPendingCount => _deadLetter.Reader.Count;
}
