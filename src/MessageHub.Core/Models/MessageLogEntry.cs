namespace MessageHub.Core;

public sealed record MessageLogEntry(
    Guid Id,
    DateTimeOffset Timestamp,
    string TenantId,
    string Channel,
    MessageDirection Direction,
    DeliveryStatus Status,
    string TargetId,
    string? TargetDisplayName,
    string Content,
    string Source,
    string? Details = null);
