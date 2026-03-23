using MessageHub.Core.Models;

namespace MessageHub.Core;

/// <summary>
/// 頻道設定服務介面 — 提供頻道設定的讀寫、型別定義與檔案路徑查詢。
/// <para>
/// 本介面僅關注頻道設定相關操作（ISP），不繼承 <see cref="ICommonParameterProvider"/>。
/// 若需要通用參數查詢，請直接注入 <see cref="ICommonParameterProvider"/>。
/// </para>
/// </summary>
public interface IChannelSettingsService
{
    Task<ChannelConfig> GetAsync(CancellationToken cancellationToken = default);
    Task<ChannelConfig> SaveAsync(ChannelConfig config, CancellationToken cancellationToken = default);
    IReadOnlyList<ChannelTypeDefinition> GetChannelTypes();
    string GetSettingsFilePath();
}
