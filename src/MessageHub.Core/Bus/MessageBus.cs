using System.Threading.Channels;
using MessageHub.Core.Models;

namespace MessageHub.Core.Bus;

/// <summary>
/// 訊息匯流排實作 — 使用 System.Threading.Channels 提供高性能的生產者/消費者模型。
/// 對應 MESSAGE_BUS_ARCHITECTURE 規格文件中的 MessageBus。
/// 提供三條獨立的無界佇列：
/// <list type="bullet">
///   <item><description>Outbound：由系統發送給外部頻道的出站訊息佇列。</description></item>
///   <item><description>Inbound：從外部頻道接收並進入系統的入站訊息佇列。</description></item>
///   <item><description>Dead Letter Queue (DLQ)：所有重試仍失敗的訊息會移至此佇列，供監控與後續處理。</description></item>
/// </list>
/// 使用 <see cref="UnboundedChannelOptions"/> 允許多個生產者與消費者同時存取，不設容量上限。
/// </summary>
internal sealed class MessageBus : IMessageBus
{
    // Outbound 佇列：存放待發送至各頻道的出站訊息
    // SingleReader/SingleWriter 皆為 false，允許多個 ChannelManager 消費者並行讀取（水平擴展）
    private readonly Channel<OutboundMessage> _outbound = Channel.CreateUnbounded<OutboundMessage>(
        new UnboundedChannelOptions { SingleReader = false, SingleWriter = false });

    // Inbound 佇列：存放從各頻道 Webhook 接收到的入站訊息
    private readonly Channel<InboundMessage> _inbound = Channel.CreateUnbounded<InboundMessage>(
        new UnboundedChannelOptions { SingleReader = false, SingleWriter = false });

    // Dead Letter Queue：存放所有重試仍失敗的訊息，保留供監控儀表板或人工介入使用
    private readonly Channel<DeadLetterMessage> _deadLetter = Channel.CreateUnbounded<DeadLetterMessage>(
        new UnboundedChannelOptions { SingleReader = false, SingleWriter = false });

    // ── Outbound 佇列操作 ────────────────────────────────────────────────────

    /// <summary>
    /// 將出站訊息非同步寫入 Outbound 佇列，由背景服務 ChannelManager 消費並發送。
    /// </summary>
    /// <param name="message">要發送的出站訊息。</param>
    /// <param name="cancellationToken">用於取消非同步操作的取消權杖。</param>
    /// <returns>代表寫入操作的 <see cref="ValueTask"/>。</returns>
    public ValueTask PublishOutboundAsync(OutboundMessage message, CancellationToken cancellationToken = default)
        => _outbound.Writer.WriteAsync(message, cancellationToken);

    /// <summary>
    /// 以非同步串流方式持續消費 Outbound 佇列中的出站訊息。
    /// 此方法會持續阻塞直到 <paramref name="cancellationToken"/> 被取消為止。
    /// </summary>
    /// <param name="cancellationToken">用於停止消費迴圈的取消權杖，通常由 BackgroundService 的 stoppingToken 傳入。</param>
    /// <returns>出站訊息的非同步串流，每次 <c>yield return</c> 一則訊息。</returns>
    public async IAsyncEnumerable<OutboundMessage> ConsumeOutboundAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // ReadAllAsync 會持續等待新訊息，直到 Channel 關閉或 CancellationToken 被取消
        await foreach (var message in _outbound.Reader.ReadAllAsync(cancellationToken))
            yield return message;
    }

    // ── Inbound 佇列操作 ─────────────────────────────────────────────────────

    /// <summary>
    /// 將入站訊息非同步寫入 Inbound 佇列，由訊息處理器（IMessageProcessor）消費。
    /// </summary>
    /// <param name="message">從頻道 Webhook 接收到的入站訊息。</param>
    /// <param name="cancellationToken">用於取消非同步操作的取消權杖。</param>
    /// <returns>代表寫入操作的 <see cref="ValueTask"/>。</returns>
    public ValueTask PublishInboundAsync(InboundMessage message, CancellationToken cancellationToken = default)
        => _inbound.Writer.WriteAsync(message, cancellationToken);

    /// <summary>
    /// 以非同步串流方式持續消費 Inbound 佇列中的入站訊息。
    /// 此方法會持續阻塞直到 <paramref name="cancellationToken"/> 被取消為止。
    /// </summary>
    /// <param name="cancellationToken">用於停止消費迴圈的取消權杖。</param>
    /// <returns>入站訊息的非同步串流，每次 <c>yield return</c> 一則訊息。</returns>
    public async IAsyncEnumerable<InboundMessage> ConsumeInboundAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var message in _inbound.Reader.ReadAllAsync(cancellationToken))
            yield return message;
    }

    // ── Dead Letter Queue 操作 ───────────────────────────────────────────────

    /// <summary>
    /// 將死信訊息（重試仍失敗的訊息）寫入 Dead Letter Queue。
    /// 通常由 ChannelManager 在所有重試耗盡後呼叫。
    /// </summary>
    /// <param name="message">包含原始訊息與失敗原因的死信訊息。</param>
    /// <param name="cancellationToken">用於取消非同步操作的取消權杖。</param>
    /// <returns>代表寫入操作的 <see cref="ValueTask"/>。</returns>
    public ValueTask PublishDeadLetterAsync(DeadLetterMessage message, CancellationToken cancellationToken = default)
        => _deadLetter.Writer.WriteAsync(message, cancellationToken);

    /// <summary>
    /// 以非同步串流方式持續消費 Dead Letter Queue 中的死信訊息。
    /// 可供監控服務或人工介入流程使用。
    /// </summary>
    /// <param name="cancellationToken">用於停止消費迴圈的取消權杖。</param>
    /// <returns>死信訊息的非同步串流，每次 <c>yield return</c> 一則訊息。</returns>
    public async IAsyncEnumerable<DeadLetterMessage> ConsumeDeadLetterAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var message in _deadLetter.Reader.ReadAllAsync(cancellationToken))
            yield return message;
    }

    /// <summary>目前 Outbound 佇列中的訊息數量（用於監控儀表板）。</summary>
    public int OutboundPendingCount => _outbound.Reader.Count;

    /// <summary>目前 Inbound 佇列中的訊息數量（用於監控儀表板）。</summary>
    public int InboundPendingCount => _inbound.Reader.Count;

    /// <summary>目前 DLQ 佇列中的訊息數量（用於監控儀表板）。</summary>
    public int DeadLetterPendingCount => _deadLetter.Reader.Count;
}
