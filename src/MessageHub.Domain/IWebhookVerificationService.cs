using MessageHub.Core.Models;

namespace MessageHub.Domain;

/// <summary>
/// Webhook 驗證服務介面 — 負責驗證指定頻道的 Webhook 連線是否正常。
/// </summary>
public interface IWebhookVerificationService
{
    /// <summary>
    /// 非同步驗證指定頻道的 Webhook 連線狀態。
    /// </summary>
    /// <param name="channelId">要驗證的頻道識別碼（如 <c>telegram</c>、<c>line</c>）。</param>
    /// <param name="cancellationToken">用於取消非同步操作的權杖。</param>
    /// <returns>
    /// 包含驗證結果的 <see cref="WebhookVerifyResult"/>，
    /// 其中包括是否成功以及相關訊息。
    /// </returns>
    Task<WebhookVerifyResult> VerifyAsync(string channelId, CancellationToken cancellationToken = default);
}
