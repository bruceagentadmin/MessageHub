namespace MessageHub.Core.Models;

public sealed record OutboundMessage(
    string TenantId,
    string Channel,
    string ChatId,
    string Content,
    object? Metadata = null);
