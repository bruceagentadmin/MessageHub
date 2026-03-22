using System.Net.Http.Json;
using MessageHub.Core.Models;

namespace MessageHub.Core.Channels;

public sealed class WebhookVerificationService(IChannelSettingsService channelSettingsService) : IWebhookVerificationService
{
    private readonly HttpClient _httpClient = new();

    public async Task<WebhookVerifyResult> VerifyAsync(string channelId, CancellationToken cancellationToken = default)
    {
        var config = await channelSettingsService.GetAsync(cancellationToken);
        config.Channels.TryGetValue(channelId, out var channel);

        if (channel is null)
        {
            return new WebhookVerifyResult(channelId, "Unknown", false, "none", null, "找不到指定頻道設定。");
        }

        var channelType = channelId;

        var webhookUrl = channel.Parameters.GetValueOrDefault("WebhookUrl")?.Trim();
        if (string.IsNullOrWhiteSpace(webhookUrl))
        {
            return new WebhookVerifyResult(channelId, channelType, false, "none", null, "WebhookUrl 未設定。", null);
        }

        if (channelType.Equals("Line", StringComparison.OrdinalIgnoreCase))
        {
            using var response = await _httpClient.PostAsJsonAsync(webhookUrl, new { destination = "verify", events = Array.Empty<object>() }, cancellationToken);
            return new WebhookVerifyResult(
                channelId,
                channelType,
                response.IsSuccessStatusCode,
                "verify",
                (int)response.StatusCode,
                response.IsSuccessStatusCode ? "LINE webhook 驗證成功（收到 HTTP 200 範圍回應）。" : "LINE webhook 驗證失敗。",
                webhookUrl);
        }

        if (channelType.Equals("Telegram", StringComparison.OrdinalIgnoreCase))
        {
            var botToken = channel.Parameters.GetValueOrDefault("BotToken")?.Trim();
            if (string.IsNullOrWhiteSpace(botToken))
            {
                return new WebhookVerifyResult(channelId, channelType, false, "bind", null, "BotToken 未設定。", webhookUrl);
            }

            var apiUrl = $"https://api.telegram.org/bot{botToken}/setWebhook?url={Uri.EscapeDataString(webhookUrl)}";
            using var response = await _httpClient.GetAsync(apiUrl, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            return new WebhookVerifyResult(
                channelId,
                channelType,
                response.IsSuccessStatusCode,
                "bind",
                (int)response.StatusCode,
                response.IsSuccessStatusCode ? "Telegram webhook 綁定成功。" : "Telegram webhook 綁定失敗。",
                webhookUrl,
                body);
        }

        return new WebhookVerifyResult(channelId, channelType, false, "none", null, "目前只支援 Line / Telegram 驗證。", webhookUrl);
    }
}
