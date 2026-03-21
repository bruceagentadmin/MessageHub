using MessageHub.Application;
using MessageHub.Domain;
using Microsoft.AspNetCore.Mvc;

namespace MessageHub.Api.Controllers;

[ApiController]
[Route("api/control")]
public sealed class ControlCenterController(
    IMessageOrchestrator orchestrator,
    IChannelSettingsService channelSettingsService,
    IWebhookVerificationService webhookVerificationService) : ControllerBase
{
    [HttpGet("channels")]
    public ActionResult<IReadOnlyList<ChannelDefinition>> GetChannels()
        => Ok(orchestrator.GetChannels());

    [HttpGet("logs")]
    public async Task<ActionResult<IReadOnlyList<MessageLogEntry>>> GetLogs([FromQuery] int count = 50, CancellationToken cancellationToken = default)
        => Ok(await orchestrator.GetRecentLogsAsync(count, cancellationToken));

    [HttpPost("send")]
    public async Task<ActionResult<MessageLogEntry>> Send([FromBody] SendMessageRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TenantId) || string.IsNullOrWhiteSpace(request.Channel) || string.IsNullOrWhiteSpace(request.TargetId) || string.IsNullOrWhiteSpace(request.Content))
        {
            return BadRequest("tenantId, channel, targetId, content 必填");
        }

        var result = await orchestrator.SendManualAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("channel-settings")]
    public async Task<ActionResult<object>> GetChannelSettings(CancellationToken cancellationToken)
        => Ok(new
        {
            filePath = channelSettingsService.GetSettingsFilePath(),
            channelTypes = channelSettingsService.GetChannelTypes(),
            settings = await channelSettingsService.GetAsync(cancellationToken)
        });

    [HttpPost("channel-settings")]
    public async Task<ActionResult<ChannelSettingsDocument>> SaveChannelSettings([FromBody] ChannelSettingsDocument document, CancellationToken cancellationToken)
    {
        var result = await channelSettingsService.SaveAsync(document, cancellationToken);
        return Ok(result);
    }

    [HttpPost("channel-settings/verify-webhook")]
    public async Task<ActionResult<WebhookVerifyResult>> VerifyWebhook([FromBody] WebhookVerifyRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ChannelId))
        {
            return BadRequest("channelId 必填");
        }

        var result = await webhookVerificationService.VerifyAsync(request.ChannelId, cancellationToken);
        return Ok(result);
    }
}
