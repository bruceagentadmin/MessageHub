using System.Text.Json;
using MessageHub.Application;
using MessageHub.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace MessageHub.Api.Controllers;

[ApiController]
[Route("api/line")]
public sealed class LineWebhookController(UnifiedMessageProcessor processor, ILogger<LineWebhookController> logger) : ControllerBase
{
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
            var request = new WebhookTextMessageRequest(userId, userId, text);
            await processor.HandleInboundAsync("line-default", "line", request, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Line webhook 處理失敗");
        }

        return Ok();
    }
}
