using MessageHub.Core;
using Microsoft.AspNetCore.Mvc;

namespace MessageHub.Api.Controllers;

[ApiController]
[Route("api/control")]
public sealed class ControlCenterController(
    UnifiedMessageProcessor processor,
    IChannelSettingsService channelSettingsService,
    IWebhookVerificationService webhookVerificationService) : ControllerBase
{
    [HttpGet("channels")]
    public ActionResult<IReadOnlyList<ChannelDefinition>> GetChannels()
        => Ok(processor.GetChannels());

    [HttpGet("logs")]
    public async Task<ActionResult<IReadOnlyList<MessageLogEntry>>> GetLogs([FromQuery] int count = 50, CancellationToken cancellationToken = default)
        => Ok(await processor.GetRecentLogsAsync(count, cancellationToken));

    [HttpPost("send")]
    public async Task<ActionResult<MessageLogEntry>> Send([FromBody] SendMessageRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TenantId) || string.IsNullOrWhiteSpace(request.Channel) || string.IsNullOrWhiteSpace(request.Content))
        {
            return BadRequest("tenantId, channel, content 必填；targetId 可留空以使用最近互動對象");
        }

        var result = await processor.SendManualAsync(request, cancellationToken);
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
    public async Task<ActionResult<ChannelConfig>> SaveChannelSettings([FromBody] ChannelConfig config, CancellationToken cancellationToken)
    {
        var result = await channelSettingsService.SaveAsync(config, cancellationToken);
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
