using System.Net.Http.Json;
using MessageHub.Core;

namespace MessageHub.Infrastructure;

public sealed class WebhookVerificationService(IChannelSettingsService channelSettingsService) : IWebhookVerificationService
{
    private readonly HttpClient _httpClient = new();

    public async Task<WebhookVerifyResult> VerifyAsync(string channelId, CancellationToken cancellationToken = default)
    {
        var config = await channelSettingsService.GetAsync(cancellationToken);
        var channel = config.Channels.FirstOrDefault(x => x.Id.Equals(channelId, StringComparison.OrdinalIgnoreCase));

        if (channel is null)
        {
            return new WebhookVerifyResult(channelId, "Unknown", false, "none", null, "找不到指定頻道設定。");
        }

        var webhookUrl = channel.Parameters.GetValueOrDefault("WebhookUrl")?.Trim();
        if (string.IsNullOrWhiteSpace(webhookUrl))
        {
            return new WebhookVerifyResult(channel.Id, channel.Type, false, "none", null, "WebhookUrl 未設定。", null);
        }

        if (channel.Type.Equals("Line", StringComparison.OrdinalIgnoreCase))
        {
            using var response = await _httpClient.PostAsJsonAsync(webhookUrl, new { destination = "verify", events = Array.Empty<object>() }, cancellationToken);
            return new WebhookVerifyResult(
                channel.Id,
                channel.Type,
                response.IsSuccessStatusCode,
                "verify",
                (int)response.StatusCode,
                response.IsSuccessStatusCode ? "LINE webhook 驗證成功（收到 HTTP 200 範圍回應）。" : "LINE webhook 驗證失敗。",
                webhookUrl);
        }

        if (channel.Type.Equals("Telegram", StringComparison.OrdinalIgnoreCase))
        {
            var botToken = channel.Parameters.GetValueOrDefault("BotToken")?.Trim();
            if (string.IsNullOrWhiteSpace(botToken))
            {
                return new WebhookVerifyResult(channel.Id, channel.Type, false, "bind", null, "BotToken 未設定。", webhookUrl);
            }

            var apiUrl = $"https://api.telegram.org/bot{botToken}/setWebhook?url={Uri.EscapeDataString(webhookUrl)}";
            using var response = await _httpClient.GetAsync(apiUrl, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            return new WebhookVerifyResult(
                channel.Id,
                channel.Type,
                response.IsSuccessStatusCode,
                "bind",
                (int)response.StatusCode,
                response.IsSuccessStatusCode ? "Telegram webhook 綁定成功。" : "Telegram webhook 綁定失敗。",
                webhookUrl,
                body);
        }

        return new WebhookVerifyResult(channel.Id, channel.Type, false, "none", null, "目前只支援 Line / Telegram 驗證。", webhookUrl);
    }
}
