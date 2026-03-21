namespace MessageHub.Domain;

public sealed record WebhookVerifyRequest(string ChannelId);

public sealed record WebhookVerifyResult(
    string ChannelId,
    string ChannelType,
    bool Success,
    string Action,
    int? StatusCode,
    string Message,
    string? WebhookUrl = null,
    string? Details = null);
