using MessageHub.Core;
using MessageHub.Core.Models;
using MessageHub.Domain.Models;
using MessageHub.Domain.Repositories;

namespace MessageHub.Domain.Services;

/// <summary>
/// 訊息收發 Service 實作 — 編排 Core 層功能並加入持久化邏輯。
/// 收到 inbound 訊息時，除了委派 Core Coordinator 處理通訊外，也更新聯絡人記錄。
/// </summary>
public sealed class MessagingService(
    IMessageCoordinator coordinator,
    IChannelSettingsService channelSettingsService,
    IWebhookVerificationService webhookVerificationService,
    IContactRepository contactRepository) : IMessagingService
{
    // ── 訊息協調 ──

    public async Task<MessageLogEntry> HandleInboundAsync(string tenantId, string channel, WebhookTextMessageRequest request, CancellationToken cancellationToken = default)
    {
        var result = await coordinator.HandleInboundAsync(tenantId, channel, request, cancellationToken);

        // 更新聯絡人記錄
        var existing = await contactRepository.FindAsync(channel, request.SenderId, cancellationToken);
        var contact = existing is null
            ? new Contact(Guid.NewGuid(), channel, request.SenderId, request.SenderId,
                          request.ChatId, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 1)
            : existing with
            {
                DisplayName = request.SenderId,
                ChatId = request.ChatId,
                LastSeenAt = DateTimeOffset.UtcNow,
                MessageCount = existing.MessageCount + 1
            };
        await contactRepository.UpsertAsync(contact, cancellationToken);

        return result;
    }

    public Task<MessageLogEntry> SendManualAsync(SendMessageRequest request, CancellationToken cancellationToken = default)
        => coordinator.SendManualAsync(request, cancellationToken);

    public Task<IReadOnlyList<MessageLogEntry>> GetRecentLogsAsync(int count, CancellationToken cancellationToken = default)
        => coordinator.GetRecentLogsAsync(count, cancellationToken);

    public IReadOnlyList<ChannelDefinition> GetChannels()
        => coordinator.GetChannels();

    // ── 頻道設定 ──

    public Task<ChannelConfig> GetChannelSettingsAsync(CancellationToken cancellationToken = default)
        => channelSettingsService.GetAsync(cancellationToken);

    public Task<ChannelConfig> SaveChannelSettingsAsync(ChannelConfig config, CancellationToken cancellationToken = default)
        => channelSettingsService.SaveAsync(config, cancellationToken);

    public IReadOnlyList<ChannelTypeDefinition> GetChannelTypes()
        => channelSettingsService.GetChannelTypes();

    public string GetSettingsFilePath()
        => channelSettingsService.GetSettingsFilePath();

    // ── Webhook 驗證 ──

    public Task<WebhookVerifyResult> VerifyWebhookAsync(string channelId, CancellationToken cancellationToken = default)
        => webhookVerificationService.VerifyAsync(channelId, cancellationToken);
}
