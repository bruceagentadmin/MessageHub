namespace MessageHub.Core.Models;

public sealed record WebhookTextMessageRequest(
    string ChatId,
    string SenderId,
    string Content);
