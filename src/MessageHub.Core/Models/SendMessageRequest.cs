namespace MessageHub.Core.Models;

public sealed record SendMessageRequest(
    string TenantId,
    string Channel,
    string TargetId,
    string Content,
    string? TriggeredBy);
