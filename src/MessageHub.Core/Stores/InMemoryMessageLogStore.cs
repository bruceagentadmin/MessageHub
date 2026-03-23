using System.Collections.Concurrent;
using MessageHub.Core.Models;

namespace MessageHub.Core.Stores;

/// <summary>
/// 以記憶體為基礎的訊息日誌儲存實作，使用 <see cref="ConcurrentQueue{T}"/> 達成執行緒安全。
/// 最多保留 500 筆最新的訊息日誌記錄；超過上限時，自動從佇列前端移除最舊的項目。
/// 此實作為應用程式生命週期內的暫存，服務重啟後資料將清空。
/// </summary>
internal sealed class InMemoryMessageLogStore : IMessageLogStore
{
    // 使用 ConcurrentQueue 保證多執行緒下的 Enqueue/Dequeue 操作安全
    private readonly ConcurrentQueue<MessageLogEntry> _entries = new();

    /// <inheritdoc />
    /// <summary>
    /// 將一筆訊息日誌記錄加入佇列末端。
    /// 若目前記錄數超過 500 筆，則從佇列前端（最舊的記錄）持續移除，直到恢復上限內。
    /// </summary>
    /// <param name="entry">要新增的訊息日誌記錄。</param>
    /// <param name="cancellationToken">取消權杖（此實作不涉及 I/O，實際上不會使用）。</param>
    /// <returns>已完成的 <see cref="Task"/>。</returns>
    public Task AddAsync(MessageLogEntry entry, CancellationToken cancellationToken = default)
    {
        // 將新記錄推入佇列末端
        _entries.Enqueue(entry);

        // 滾動視窗策略：超過容量上限 500 時，從最舊的一端移除
        // TryDequeue 是執行緒安全的操作，out _ 表示捨棄被移除的值
        while (_entries.Count > 500)
        {
            _entries.TryDequeue(out _);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    /// <summary>
    /// 從佇列中取得最近的 <paramref name="count"/> 筆記錄（由新到舊排序）。
    /// </summary>
    /// <param name="count">
    /// 要取回的最大筆數。實際回傳數量受 <see cref="Math.Clamp"/> 限制，
    /// 最小為 1、最大為 200。
    /// </param>
    /// <param name="cancellationToken">取消權杖（此實作不涉及 I/O，實際上不會使用）。</param>
    /// <returns>依時間倒序排列的訊息日誌記錄清單。</returns>
    public Task<IReadOnlyList<MessageLogEntry>> GetRecentAsync(int count, CancellationToken cancellationToken = default)
    {
        // Reverse() 讓最新的記錄排在最前面
        // Math.Clamp 防止 count 為 0 或超過 200 的異常輸入
        var items = _entries
            .Reverse()
            .Take(Math.Clamp(count, 1, 200))
            .ToArray();

        return Task.FromResult<IReadOnlyList<MessageLogEntry>>(items);
    }
}
