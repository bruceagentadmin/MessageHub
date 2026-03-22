namespace MessageHub.Core.Models;

/// <summary>
/// 頻道總體配置 — 映射租戶的 JSON 頻道設定。
/// 對應規格文件中的 ChannelConfig。
/// </summary>
public sealed class ChannelConfig
{
    public Guid TenantId { get; set; }
    public Dictionary<string, ChannelSettings> Channels { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
