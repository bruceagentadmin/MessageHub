namespace MessageHub.Core;

/// <summary>
/// 頻道設定服務介面 — 提供頻道設定的讀寫、型別定義與檔案路徑查詢。
/// </summary>
public interface IChannelSettingsService : ICommonParameterProvider
{
    Task<ChannelConfig> GetAsync(CancellationToken cancellationToken = default);
    Task<ChannelConfig> SaveAsync(ChannelConfig config, CancellationToken cancellationToken = default);
    IReadOnlyList<ChannelTypeDefinition> GetChannelTypes();
    string GetSettingsFilePath();
}
