namespace MessageHub.Core;

/// <summary>
/// 頻道個別設定 — 存放單一頻道的開關與參數。
/// 對應規格文件中的 ChannelSettings。
/// </summary>
public sealed class ChannelSettings
{
    public bool Enabled { get; set; }
    public Dictionary<string, string> Parameters { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
