namespace MessageHub.Core;

/// <summary>
/// 通知服務介面 — 定義系統主動發送通知的標準行為。
/// 對應規格文件中的 INotificationService。
/// </summary>
public interface INotificationService
{
    /// <summary>根據租戶與頻道發送通知文字</summary>
    Task SendNotificationAsync(string tenantId, string channel, string message, CancellationToken cancellationToken = default);
}
