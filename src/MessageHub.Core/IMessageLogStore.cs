using MessageHub.Core.Models;

namespace MessageHub.Core;

/// <summary>
/// 訊息紀錄儲存介面 — 提供訊息日誌的寫入與查詢功能，
/// 用於追蹤所有進出頻道的訊息歷程。
/// </summary>
public interface IMessageLogStore
{
    /// <summary>
    /// 非同步新增一筆訊息日誌紀錄。
    /// </summary>
    /// <param name="entry">要新增的 <see cref="MessageLogEntry"/> 訊息紀錄物件。</param>
    /// <param name="cancellationToken">用於取消非同步操作的權杖。</param>
    Task AddAsync(MessageLogEntry entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// 非同步取得最近的訊息日誌紀錄清單。
    /// </summary>
    /// <param name="count">要取得的最大筆數。</param>
    /// <param name="cancellationToken">用於取消非同步操作的權杖。</param>
    /// <returns>
    /// 依時間倒序排列的 <see cref="MessageLogEntry"/> 唯讀清單，
    /// 清單長度不超過 <paramref name="count"/> 筆。
    /// </returns>
    Task<IReadOnlyList<MessageLogEntry>> GetRecentAsync(int count, CancellationToken cancellationToken = default);
}
