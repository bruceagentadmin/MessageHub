namespace MessageHub.Core;

public sealed record OutboundMessage(
    string TenantId,
    string Channel,
    string ChatId,
    string Content,
    object? Metadata = null);
