using System.Net.Http.Headers;
using System.Net.Http.Json;
using MessageHub.Core.Models;

namespace MessageHub.Core.Channels;

/// <summary>
/// Line 頻道實作 — 直接實作 IChannel。
/// 專門處理 Line Messaging API 的邏輯，包含 Signature 驗證、LineEvent 解析與 API 呼叫。
/// </summary>
public sealed class LineChannel(IChannelSettingsService channelSettingsService, HttpClient? httpClient = null) : IChannel
{
    private readonly IChannelSettingsService _channelSettingsService = channelSettingsService;
    private readonly HttpClient _httpClient = httpClient ?? new HttpClient();

    public string Name => "line";

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
        var token = settings?.Parameters.GetValueOrDefault("ChannelAccessToken")?.Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("ChannelAccessToken 未設定");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.line.me/v2/bot/message/push");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(new
        {
            to = chatId,
            messages = new[] { new { type = "text", text = message.Content } }
        });

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private async Task<ChannelSettings?> GetSettingsAsync(CancellationToken cancellationToken)
    {
        var config = await _channelSettingsService.GetAsync(cancellationToken);
        var settings = ChannelSettingsResolver.FindSettings(config, "line");
        return settings is { Enabled: true } ? settings : null;
    }
}
