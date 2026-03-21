using System.Text.Json;
using MessageHub.Application;
using MessageHub.Domain;
using Microsoft.AspNetCore.Mvc;

namespace MessageHub.Api.Controllers;

[ApiController]
[Route("api/telegram")]
public sealed class TelegramWebhookController(IMessageLogStore logStore, IRecentTargetStore recentTargetStore) : ControllerBase
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
                {
                    chatId = chatIdElement.ToString();
                }

                if (message.TryGetProperty("text", out var textElement))
                {
                    text = textElement.GetString();
                }

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
        }

        if (!string.IsNullOrWhiteSpace(chatId))
        {
            await recentTargetStore.SetLastTargetAsync("telegram", chatId, displayName, cancellationToken);
        }

        await logStore.AddAsync(new MessageLogEntry(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            "telegram-default",
            "telegram",
            MessageDirection.Inbound,
            DeliveryStatus.Delivered,
            chatId ?? "telegram-unknown",
            text ?? "[Telegram webhook verify / non-message event]",
            "telegram webhook",
            string.IsNullOrWhiteSpace(displayName) ? null : $"DisplayName={displayName}"), cancellationToken);

        return Ok();
    }
}
