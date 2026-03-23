using MessageHub.Core.Models;

namespace MessageHub.Core;

/// <summary>
/// 最近互動對象儲存介面 — 記錄每個頻道最後一次互動的目標資訊，
/// 用於手動發送訊息時自動推斷目標對象。
/// </summary>
public interface IRecentTargetStore
{
    /// <summary>
    /// 非同步設定指定頻道的最近互動目標。
    /// </summary>
    /// <param name="channel">頻道識別碼（如 <c>telegram</c>、<c>line</c>）。</param>
    /// <param name="targetId">目標的唯一識別碼（如 ChatId 或 UserId）。</param>
    /// <param name="displayName">目標的顯示名稱，選填。</param>
    /// <param name="cancellationToken">用於取消非同步操作的權杖。</param>
    Task SetLastTargetAsync(string channel, string targetId, string? displayName = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 非同步取得指定頻道最近互動的目標資訊。
    /// </summary>
    /// <param name="channel">頻道識別碼（如 <c>telegram</c>、<c>line</c>）。</param>
    /// <param name="cancellationToken">用於取消非同步操作的權杖。</param>
    /// <returns>
    /// 若存在記錄，回傳 <see cref="RecentTargetInfo"/>；
    /// 否則回傳 <see langword="null"/>。
    /// </returns>
    Task<RecentTargetInfo?> GetLastTargetAsync(string channel, CancellationToken cancellationToken = default);
}
