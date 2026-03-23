using MessageHub.Core.Models;

namespace MessageHub.Core;

/// <summary>
/// 訊息協調器介面 — 定義 Webhook 進站處理、手動發送、日誌查詢與頻道清單等協調操作。
/// <para>
/// 與 <see cref="IMessageProcessor"/> 不同，本介面聚焦於「訊息流的調度與協調」，
/// 包含進站訊息處理（解析 → 記錄 → 推送至 Bus）和手動發送等高層操作。
/// </para>
/// </summary>
public interface IMessageCoordinator
{
    /// <summary>
    /// 處理 Webhook 進站訊息 — 解析請求、記錄日誌、產生自動回覆並推送至 MessageBus。
    /// </summary>
    /// <param name="tenantId">租戶識別碼。</param>
    /// <param name="channel">頻道名稱（如 "telegram"、"line"）。</param>
    /// <param name="request">來自 Webhook 的文字訊息請求。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>記錄進站訊息後產生的 <see cref="MessageLogEntry"/>。</returns>
    Task<MessageLogEntry> HandleInboundAsync(string tenantId, string channel, WebhookTextMessageRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 手動發送訊息 — 從控制中心發起，解析目標後推送至 MessageBus。
    /// </summary>
    /// <param name="request">發送訊息請求，包含租戶、頻道、目標與內容。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>記錄發送請求後產生的 <see cref="MessageLogEntry"/>（狀態為 Pending 或 Failed）。</returns>
    Task<MessageLogEntry> SendManualAsync(SendMessageRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得最近的訊息日誌。
    /// </summary>
    /// <param name="count">欲取得的日誌數量上限。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>按時間倒序排列的訊息日誌清單。</returns>
    Task<IReadOnlyList<MessageLogEntry>> GetRecentLogsAsync(int count, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得所有已註冊頻道的定義清單。
    /// </summary>
    /// <returns>頻道定義的唯讀清單。</returns>
    IReadOnlyList<ChannelDefinition> GetChannels();
}
