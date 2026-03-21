using MessageHub.Domain;

namespace MessageHub.Application;

public interface IWebhookVerificationService
{
    Task<WebhookVerifyResult> VerifyAsync(string channelId, CancellationToken cancellationToken = default);
}
