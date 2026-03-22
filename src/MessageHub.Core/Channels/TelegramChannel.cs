using System.Net.Http.Json;

namespace MessageHub.Core;

/// <summary>
/// Telegram 頻道實作 — 直接實作 IChannel。
/// 專門處理 Telegram Bot API 的邏輯，包含 Update 解析與 Bot Client 發送訊息。
/// </summary>
public sealed class TelegramChannel(IChannelSettingsService channelSettingsService, HttpClient? httpClient = null) : IChannel
{
    private readonly IChannelSettingsService _channelSettingsService = channelSettingsService;
    private readonly HttpClient _httpClient = httpClient ?? new HttpClient();

    public string Name => "telegram";

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

    public async Task SendAsync(string chatId, OutboundMessage message, ChannelSettings? settings = null, CancellationToken cancellationToken = default)
    {
        settings ??= await GetSettingsAsync(cancellationToken);
        var botToken = settings?.Parameters.GetValueOrDefault("BotToken")?.Trim();
        if (string.IsNullOrWhiteSpace(botToken))
        {
            throw new InvalidOperationException("BotToken 未設定");
        }

        var payload = new { chat_id = chatId, text = message.Content };
        using var response = await _httpClient.PostAsJsonAsync($"https://api.telegram.org/bot{botToken}/sendMessage", payload, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private async Task<ChannelSettings?> GetSettingsAsync(CancellationToken cancellationToken)
    {
        var config = await _channelSettingsService.GetAsync(cancellationToken);
        var settings = ChannelSettingsResolver.FindSettings(config, "telegram");
        return settings is { Enabled: true } ? settings : null;
    }
}
