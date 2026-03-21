using System.Text.Json;
using MessageHub.Application;
using MessageHub.Domain;
using Microsoft.AspNetCore.Mvc;

namespace MessageHub.Api.Controllers;

[ApiController]
[Route("api/telegram")]
public sealed class TelegramWebhookController(IMessageLogStore logStore) : ControllerBase
{
    [HttpPost("webhook")]
    public async Task<IActionResult> Handle([FromBody] JsonElement data, CancellationToken cancellationToken)
    {
        string? chatId = null;
        string? text = null;

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
            }
        }
        catch
        {
            // POC 階段先保證 webhook 不因解析錯誤而回非 200。
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
            "telegram webhook"), cancellationToken);

        return Ok();
    }
}
