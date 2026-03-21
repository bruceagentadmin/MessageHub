namespace MessageHub.Core;

public sealed record WebhookTextMessageRequest(
    string ChatId,
    string SenderId,
    string Content);
