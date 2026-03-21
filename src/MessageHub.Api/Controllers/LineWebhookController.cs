using System.Text.Json;
using MessageHub.Application;
using MessageHub.Domain;
using Microsoft.AspNetCore.Mvc;

namespace MessageHub.Api.Controllers;

[ApiController]
[Route("api/line")]
public sealed class LineWebhookController(IMessageLogStore logStore) : ControllerBase
{
    [HttpPost("webhook")]
    public async Task<IActionResult> Handle([FromBody] JsonElement data, CancellationToken cancellationToken)
    {
        string? userId = null;
        string? replyToken = null;
        string? text = null;

        try
        {
            if (data.TryGetProperty("events", out var events) && events.ValueKind == JsonValueKind.Array && events.GetArrayLength() > 0)
            {
                var firstEvent = events[0];

                if (firstEvent.TryGetProperty("replyToken", out var replyTokenElement))
                {
                    replyToken = replyTokenElement.GetString();
                }

                if (firstEvent.TryGetProperty("source", out var source) && source.TryGetProperty("userId", out var userIdElement))
                {
                    userId = userIdElement.GetString();
                }

                if (firstEvent.TryGetProperty("message", out var message) && message.TryGetProperty("text", out var textElement))
                {
                    text = textElement.GetString();
                }
            }
        }
        catch
        {
            // 驗證階段先保守吃掉解析錯誤，確保 LINE 收到 200。
        }

        await logStore.AddAsync(new MessageLogEntry(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            "line-default",
            "line",
            MessageDirection.Inbound,
            DeliveryStatus.Delivered,
            userId ?? "line-unknown",
            text ?? "[LINE webhook verify / non-text event]",
            "line webhook",
            $"ReplyToken={(string.IsNullOrWhiteSpace(replyToken) ? "none" : "present")}"), cancellationToken);

        return Ok();
    }
}
