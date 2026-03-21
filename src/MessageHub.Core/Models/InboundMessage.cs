namespace MessageHub.Core;

public sealed record InboundMessage(
    string TenantId,
    string Channel,
    string ChatId,
    string SenderId,
    string Content,
    DateTimeOffset ReceivedAt,
    string? OriginalPayload = null);
