using System.Collections.Concurrent;
using MessageHub.Core.Models;

namespace MessageHub.Core.Stores;

/// <summary>
/// 以記憶體為基礎的最近互動對象儲存實作，使用 <see cref="ConcurrentDictionary{TKey,TValue}"/> 達成執行緒安全。
/// 每個頻道僅記錄最後一次互動的目標（例如：最後一個傳送訊息的 Chat ID），
/// 供控制中心在 <c>targetId</c> 未指定時自動選取預設發送對象。
/// 此實作為應用程式生命週期內的暫存，服務重啟後資料將清空。
/// </summary>
public sealed class RecentTargetStore : IRecentTargetStore
{
    // 以頻道名稱（不區分大小寫）作為鍵，儲存該頻道最後一次的互動目標資訊
    private readonly ConcurrentDictionary<string, RecentTargetInfo> _targets = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    /// <summary>
    /// 設定或更新指定頻道的最後互動目標。
    /// 若該頻道已有記錄，將以新值覆蓋（索引子賦值操作在 <see cref="ConcurrentDictionary{TKey,TValue}"/> 中是執行緒安全的）。
    /// </summary>
    /// <param name="channel">頻道識別碼，例如 "telegram"、"line"（不區分大小寫）。</param>
    /// <param name="targetId">互動目標的識別碼，例如 Telegram Chat ID 或 Line User ID。</param>
    /// <param name="displayName">互動目標的顯示名稱（選填），用於前端介面顯示。</param>
    /// <param name="cancellationToken">取消權杖（此實作不涉及 I/O，實際上不會使用）。</param>
    /// <returns>已完成的 <see cref="Task"/>。</returns>
    public Task SetLastTargetAsync(string channel, string targetId, string? displayName = null, CancellationToken cancellationToken = default)
    {
        // 直接以索引子賦值覆蓋舊記錄，同時記錄當前 UTC 時間作為最後互動時間戳記
        _targets[channel] = new RecentTargetInfo(channel, targetId, displayName, DateTimeOffset.UtcNow);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    /// <summary>
    /// 取得指定頻道的最後互動目標資訊。
    /// 若該頻道尚未有任何互動記錄，回傳 <c>null</c>。
    /// </summary>
    /// <param name="channel">頻道識別碼，例如 "telegram"、"line"（不區分大小寫）。</param>
    /// <param name="cancellationToken">取消權杖（此實作不涉及 I/O，實際上不會使用）。</param>
    /// <returns>
    /// 若有記錄則回傳 <see cref="RecentTargetInfo"/>；
    /// 若頻道尚未有互動記錄則回傳 <c>null</c>。
    /// </returns>
    public Task<RecentTargetInfo?> GetLastTargetAsync(string channel, CancellationToken cancellationToken = default)
    {
        // TryGetValue 在 ConcurrentDictionary 中是執行緒安全的讀取操作
        _targets.TryGetValue(channel, out var value);
        return Task.FromResult(value);
    }
}
