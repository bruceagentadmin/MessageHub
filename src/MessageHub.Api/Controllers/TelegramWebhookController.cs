using System.Text.Json;
using MessageHub.Core;
using MessageHub.Core.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace MessageHub.Api.Controllers;

[ApiController]
[Route("api/telegram")]
public sealed class TelegramWebhookController(IMessageCoordinator coordinator, ILogger<TelegramWebhookController> logger) : ControllerBase
{
    [HttpPost("webhook")]
    public async Task<IActionResult> Handle([FromBody] JsonElement data, CancellationToken cancellationToken)
    {
        string? chatId = null;
        string? text = null;
        string? displayName = null;

        try
        {
            if (data.TryGetProperty("message", out var message))
            {
                if (message.TryGetProperty("chat", out var chat) && chat.TryGetProperty("id", out var chatIdElement))
                    chatId = chatIdElement.ToString();

                if (message.TryGetProperty("text", out var textElement))
                    text = textElement.GetString();

                if (message.TryGetProperty("from", out var from))
                {
                    var firstName = from.TryGetProperty("first_name", out var fn) ? fn.GetString() : string.Empty;
                    var lastName = from.TryGetProperty("last_name", out var ln) ? ln.GetString() : string.Empty;
                    displayName = $"{firstName} {lastName}".Trim();
                }
            }
        }
        catch
        {
            // Non-message events — ignore parse failures
        }

        if (string.IsNullOrWhiteSpace(chatId) || string.IsNullOrWhiteSpace(text))
        {
            return Ok();
        }

        try
        {
            var request = new WebhookTextMessageRequest(chatId, displayName ?? chatId, text);
            await coordinator.HandleInboundAsync("telegram-default", "telegram", request, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Telegram webhook 處理失敗");
        }

        return Ok();
    }
}
