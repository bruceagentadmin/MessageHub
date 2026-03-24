namespace MessageHub.Domain;

/// <summary>
/// 通知服務介面 — 定義系統主動向指定頻道發送通知訊息的標準行為。
/// 對應規格文件中的 INotificationService。
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// 非同步透過指定頻道發送通知文字至租戶的最近互動目標。
    /// </summary>
    /// <param name="tenantId">接收通知的租戶識別碼。</param>
    /// <param name="channel">要使用的頻道識別碼（如 <c>telegram</c>、<c>line</c>）。</param>
    /// <param name="message">要發送的通知文字內容。</param>
    /// <param name="cancellationToken">用於取消非同步操作的權杖。</param>
    Task SendNotificationAsync(string tenantId, string channel, string message, CancellationToken cancellationToken = default);
}
