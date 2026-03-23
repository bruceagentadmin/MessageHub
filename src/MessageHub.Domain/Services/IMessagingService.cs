using MessageHub.Core.Models;
using MessageHub.Domain.Models;

namespace MessageHub.Domain.Services;

/// <summary>
/// 訊息收發 Service 介面 — 完整包裝 Core 層所有對外功能。
/// API 層透過此 Service 操作一切通訊與設定功能。
/// </summary>
public interface IMessagingService
{
    // ── 訊息協調（包裝 IMessageCoordinator）──

    Task<MessageLogEntry> HandleInboundAsync(string tenantId, string channel, WebhookTextMessageRequest request, CancellationToken cancellationToken = default);
    Task<MessageLogEntry> SendManualAsync(SendMessageRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MessageLogEntry>> GetRecentLogsAsync(int count, CancellationToken cancellationToken = default);
    IReadOnlyList<ChannelDefinition> GetChannels();

    // ── 頻道設定（包裝 IChannelSettingsService）──

    Task<ChannelConfig> GetChannelSettingsAsync(CancellationToken cancellationToken = default);
    Task<ChannelConfig> SaveChannelSettingsAsync(ChannelConfig config, CancellationToken cancellationToken = default);
    IReadOnlyList<ChannelTypeDefinition> GetChannelTypes();
    string GetSettingsFilePath();

    // ── Webhook 驗證（包裝 IWebhookVerificationService）──

    Task<WebhookVerifyResult> VerifyWebhookAsync(string channelId, CancellationToken cancellationToken = default);
}
