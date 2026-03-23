using MessageHub.Core.Models;

namespace MessageHub.Core.Services;

/// <summary>
/// 訊息處理器 — 實作 <see cref="IMessageProcessor"/>，負責對進站訊息執行商業邏輯並產生回覆文字。
/// <para>
/// 本類別僅關注「訊息內容處理」（SRP），不涉及協調/調度邏輯。
/// 協調職責已移至 <see cref="MessageCoordinator"/>。
/// </para>
/// </summary>
public sealed class EchoMessageProcessor : IMessageProcessor
{
    /// <inheritdoc />
    public Task<string> ProcessAsync(InboundMessage message, CancellationToken cancellationToken = default)
    {
        // POC 階段：直接回傳確認文字，後續可替換為 AI/規則引擎等處理邏輯
        return Task.FromResult($"[POC 回覆] 已收到：{message.Content}");
    }
}
