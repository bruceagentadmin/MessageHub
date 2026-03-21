namespace MessageHub.Core;

/// <summary>
/// Webhook 驗證服務介面
/// </summary>
public interface IWebhookVerificationService
{
    Task<WebhookVerifyResult> VerifyAsync(string channelId, CancellationToken cancellationToken = default);
}
