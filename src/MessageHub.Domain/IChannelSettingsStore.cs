using MessageHub.Core.Models;

namespace MessageHub.Domain;

/// <summary>
/// 頻道設定儲存介面 — 負責持久化與讀取所有頻道的組態設定，
/// 例如 Token、Webhook URL 等敏感參數。
/// </summary>
public interface IChannelSettingsStore
{
    /// <summary>
    /// 非同步載入目前的頻道組態設定。
    /// </summary>
    /// <param name="cancellationToken">用於取消非同步操作的權杖。</param>
    /// <returns>已載入的 <see cref="ChannelConfig"/> 頻道組態物件。</returns>
    Task<ChannelConfig> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 非同步儲存頻道組態設定，並回傳儲存後的最新狀態。
    /// </summary>
    /// <param name="config">要儲存的 <see cref="ChannelConfig"/> 頻道組態物件。</param>
    /// <param name="cancellationToken">用於取消非同步操作的權杖。</param>
    /// <returns>儲存成功後的 <see cref="ChannelConfig"/> 最新組態物件。</returns>
    Task<ChannelConfig> SaveAsync(ChannelConfig config, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得設定檔的儲存路徑（用於診斷與記錄）。
    /// </summary>
    /// <returns>設定檔的完整絕對路徑字串。</returns>
    string GetFilePath();
}
