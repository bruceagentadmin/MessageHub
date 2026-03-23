using MessageHub.Domain.Models;
using MessageHub.Domain.Services;
using Microsoft.AspNetCore.Mvc;

namespace MessageHub.Api.Controllers;

[ApiController]
[Route("api/history")]
public sealed class HistoryController(IHistoryService historyService) : ControllerBase
{
    [HttpGet("logs")]
    public async Task<ActionResult<PagedResult<MessageLogRecord>>> QueryLogs(
        [FromQuery] string? channel = null,
        [FromQuery] int? direction = null,
        [FromQuery] int? status = null,
        [FromQuery] string? targetId = null,
        [FromQuery] string? senderId = null,
        [FromQuery] string? keyword = null,
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var query = new MessageLogQuery(
            Channel: channel,
            Direction: direction,
            Status: status,
            TargetId: targetId,
            SenderId: senderId,
            ContentKeyword: keyword,
            From: from,
            To: to,
            Page: Math.Max(1, page),
            PageSize: Math.Clamp(pageSize, 1, 200));

        var result = await historyService.QueryLogsAsync(query, cancellationToken);
        return Ok(result);
    }
}
