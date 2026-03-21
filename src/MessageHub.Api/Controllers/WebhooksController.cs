using MessageHub.Application;
using MessageHub.Domain;
using Microsoft.AspNetCore.Mvc;

namespace MessageHub.Api.Controllers;

[ApiController]
[Route("api/webhooks/{channel}/{tenantId}")]
public sealed class WebhooksController(IMessageOrchestrator orchestrator) : ControllerBase
{
    [HttpPost("text")]
    public async Task<ActionResult<MessageLogEntry>> ReceiveText(string channel, string tenantId, [FromBody] WebhookTextMessageRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ChatId) || string.IsNullOrWhiteSpace(request.SenderId) || string.IsNullOrWhiteSpace(request.Content))
        {
            return BadRequest("chatId, senderId, content 必填");
        }

        var result = await orchestrator.HandleInboundAsync(tenantId, channel, request, cancellationToken);
        return Ok(result);
    }
}
