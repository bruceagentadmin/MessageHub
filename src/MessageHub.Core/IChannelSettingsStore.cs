using MessageHub.Core.Models;

namespace MessageHub.Core;

/// <summary>
/// 頻道設定儲存介面
/// </summary>
public interface IChannelSettingsStore
{
    Task<ChannelConfig> LoadAsync(CancellationToken cancellationToken = default);
    Task<ChannelConfig> SaveAsync(ChannelConfig config, CancellationToken cancellationToken = default);
    string GetFilePath();
}
