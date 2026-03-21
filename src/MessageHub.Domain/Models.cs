namespace MessageHub.Domain;

public enum MessageDirection
{
    Inbound,
    Outbound,
    System
}

public enum DeliveryStatus
{
    Pending,
    Delivered,
    Failed
}

public sealed record InboundMessage(
    string TenantId,
    string Channel,
    string ChatId,
    string SenderId,
    string Content,
    DateTimeOffset ReceivedAt,
    string? RawPayload = null);

public sealed record OutboundMessage(
    string TenantId,
    string Channel,
    string TargetId,
    string Content,
    DateTimeOffset CreatedAt,
    string? TriggeredBy = null);

public sealed record MessageLogEntry(
    Guid Id,
    DateTimeOffset Timestamp,
    string TenantId,
    string Channel,
    MessageDirection Direction,
    DeliveryStatus Status,
    string TargetId,
    string Content,
    string Source,
    string? Details = null);

public sealed record SendMessageRequest(
    string TenantId,
    string Channel,
    string TargetId,
    string Content,
    string? TriggeredBy);

public sealed record WebhookTextMessageRequest(
    string ChatId,
    string SenderId,
    string Content);

public sealed record ChannelDefinition(
    string Name,
    bool SupportsInbound,
    bool SupportsOutbound,
    string Description);
