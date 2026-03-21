namespace MessageHub.Core;

public sealed record OutboundMessage(
    string TenantId,
    string Channel,
    string TargetId,
    string Content,
    DateTimeOffset CreatedAt,
    string? TriggeredBy = null);
