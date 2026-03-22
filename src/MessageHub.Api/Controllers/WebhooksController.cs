using MessageHub.Core;
using MessageHub.Core.Models;
using MessageHub.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace MessageHub.Api.Controllers;

[ApiController]
[Route("api/webhooks/{channel}/{tenantId}")]
public sealed class WebhooksController(UnifiedMessageProcessor processor) : ControllerBase
{
    [HttpPost("text")]
    public async Task<ActionResult<MessageLogEntry>> ReceiveText(string channel, string tenantId, [FromBody] WebhookTextMessageRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ChatId) || string.IsNullOrWhiteSpace(request.SenderId) || string.IsNullOrWhiteSpace(request.Content))
        {
            return BadRequest("chatId, senderId, content 必填");
        }

        var result = await processor.HandleInboundAsync(tenantId, channel, request, cancellationToken);
        return Ok(result);
    }
}
