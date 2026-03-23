using System.Net.Http.Headers;
using System.Text.Json;
using MessageHub.Core;
using MessageHub.Core.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace MessageHub.Api.Controllers;

[ApiController]
[Route("api/line")]
public sealed class LineWebhookController(
    IMessageCoordinator coordinator,
    IChannelSettingsService channelSettingsService,
    ILogger<LineWebhookController> logger) : ControllerBase
{
    private static readonly HttpClient HttpClient = new();

    [HttpPost("webhook")]
    public async Task<IActionResult> Handle([FromBody] JsonElement data, CancellationToken cancellationToken)
    {
        string? userId = null;
        string? text = null;

        try
        {
            if (data.TryGetProperty("events", out var events) && events.ValueKind == JsonValueKind.Array && events.GetArrayLength() > 0)
            {
                var firstEvent = events[0];

                if (firstEvent.TryGetProperty("source", out var source) && source.TryGetProperty("userId", out var userIdElement))
                    userId = userIdElement.GetString();

                if (firstEvent.TryGetProperty("message", out var message) && message.TryGetProperty("text", out var textElement))
                    text = textElement.GetString();
            }
        }
        catch
        {
            // Non-text events — ignore parse failures
        }

        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(text))
        {
            return Ok();
        }

        try
        {
            var displayName = await GetUserProfileNameAsync(userId, cancellationToken);
            var request = new WebhookTextMessageRequest(userId, displayName, text);
            await coordinator.HandleInboundAsync("line-default", "line", request, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Line webhook 處理失敗");
        }

        return Ok();
    }

    private async Task<string> GetUserProfileNameAsync(string userId, CancellationToken cancellationToken)
    {
        try
        {
            var config = await channelSettingsService.GetAsync(cancellationToken);
            var settings = ChannelSettingsResolver.FindSettings(config, "line");
            var token = settings?.Parameters.GetValueOrDefault("ChannelAccessToken")?.Trim();

            if (string.IsNullOrWhiteSpace(token))
            {
                return userId;
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.line.me/v2/bot/profile/{Uri.EscapeDataString(userId)}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await HttpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return userId;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var profile = await JsonSerializer.DeserializeAsync<JsonElement>(stream, cancellationToken: cancellationToken);
            if (profile.TryGetProperty("displayName", out var displayNameElement))
            {
                var displayName = displayNameElement.GetString();
                if (!string.IsNullOrWhiteSpace(displayName))
                {
                    return displayName.Trim();
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Line profile lookup 失敗，改用 userId 顯示：{UserId}", userId);
        }

        return userId;
    }
}
