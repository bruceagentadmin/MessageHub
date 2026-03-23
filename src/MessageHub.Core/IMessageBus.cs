using MessageHub.Core.Models;

namespace MessageHub.Core;

/// <summary>
/// 訊息匯流排介面 — 提供 Inbound／Outbound／Dead Letter Queue 緩衝佇列的生產者／消費者模型。
/// 對應 MESSAGE_BUS_ARCHITECTURE 規格文件中的 IMessageBus。
/// </summary>
public interface IMessageBus
{
    // ── Outbound ────────────────────────────────────────────────────────────

    /// <summary>
    /// 非同步將一則外送訊息發佈到 Outbound 佇列。
    /// </summary>
    /// <param name="message">要發佈的 <see cref="OutboundMessage"/> 外送訊息物件。</param>
    /// <param name="cancellationToken">用於取消非同步操作的權杖。</param>
    ValueTask PublishOutboundAsync(OutboundMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// 以非同步串流方式持續消費 Outbound 佇列中的外送訊息。
    /// </summary>
    /// <param name="cancellationToken">用於停止消費並取消串流的權杖。</param>
    /// <returns>可非同步迭代的 <see cref="OutboundMessage"/> 序列。</returns>
    IAsyncEnumerable<OutboundMessage> ConsumeOutboundAsync(CancellationToken cancellationToken);

    // ── Inbound ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 非同步將一則入站訊息發佈到 Inbound 佇列。
    /// </summary>
    /// <param name="message">要發佈的 <see cref="InboundMessage"/> 入站訊息物件。</param>
    /// <param name="cancellationToken">用於取消非同步操作的權杖。</param>
    ValueTask PublishInboundAsync(InboundMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// 以非同步串流方式持續消費 Inbound 佇列中的入站訊息。
    /// </summary>
    /// <param name="cancellationToken">用於停止消費並取消串流的權杖。</param>
    /// <returns>可非同步迭代的 <see cref="InboundMessage"/> 序列。</returns>
    IAsyncEnumerable<InboundMessage> ConsumeInboundAsync(CancellationToken cancellationToken);

    // ── Dead Letter Queue ────────────────────────────────────────────────────

    /// <summary>
    /// 非同步將一則無法處理的訊息發佈到 Dead Letter Queue。
    /// </summary>
    /// <param name="message">要發佈的 <see cref="DeadLetterMessage"/> 死信訊息物件，通常包含失敗原因。</param>
    /// <param name="cancellationToken">用於取消非同步操作的權杖。</param>
    ValueTask PublishDeadLetterAsync(DeadLetterMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// 以非同步串流方式持續消費 Dead Letter Queue 中的死信訊息。
    /// </summary>
    /// <param name="cancellationToken">用於停止消費並取消串流的權杖。</param>
    /// <returns>可非同步迭代的 <see cref="DeadLetterMessage"/> 序列。</returns>
    IAsyncEnumerable<DeadLetterMessage> ConsumeDeadLetterAsync(CancellationToken cancellationToken);
}
