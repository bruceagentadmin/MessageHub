namespace MessageHub.Core.Models;

/// <summary>
/// 頻道總體配置 — 映射租戶的 JSON 頻道設定。
/// 對應規格文件中的 ChannelConfig。
/// </summary>
/// <remarks>
/// 此類別由 <c>JsonChannelSettingsStore</c> 從 JSON 檔案反序列化，
/// 其中 <see cref="Channels"/> 字典以不區分大小寫的頻道識別碼為鍵，
/// 對應的值為 <see cref="ChannelSettings"/> 物件。
/// </remarks>
public sealed class ChannelConfig
{
    /// <summary>
    /// 租戶的唯一識別碼（GUID 格式）。
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// 各頻道設定的字典，鍵為頻道識別字串（不區分大小寫），值為 <see cref="ChannelSettings"/>。
    /// </summary>
    public Dictionary<string, ChannelSettings> Channels { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
