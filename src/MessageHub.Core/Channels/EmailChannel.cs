namespace MessageHub.Core;

/// <summary>
/// Email 頻道實作 — 直接實作 IChannel。
/// Email 模擬通道，先以文字送達紀錄驗證流程。
/// </summary>
public sealed class EmailChannel(IChannelSettingsService channelSettingsService) : IChannel
{
    private readonly IChannelSettingsService _channelSettingsService = channelSettingsService;

    public string Name => "email";

    public Task<InboundMessage> ParseRequestAsync(string tenantId, WebhookTextMessageRequest request, CancellationToken cancellationToken = default)
    {
        var inbound = new InboundMessage(
            tenantId,
            Name,
            request.ChatId,
            request.SenderId,
            request.Content,
            DateTimeOffset.UtcNow,
            OriginalPayload: request.Content);

        return Task.FromResult(inbound);
    }

    public Task SendAsync(string chatId, OutboundMessage message, ChannelSettings? settings = null, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
