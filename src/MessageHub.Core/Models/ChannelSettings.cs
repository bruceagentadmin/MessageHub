namespace MessageHub.Core.Models;

/// <summary>
/// 頻道個別設定 — 存放單一頻道的開關與參數。
/// 對應規格文件中的 ChannelSettings。
/// </summary>
/// <remarks>
/// 作為 <see cref="ChannelConfig.Channels"/> 字典的值，
/// <see cref="Parameters"/> 字典的內容因頻道類型而異，
/// 例如 Telegram 需要 <c>BotToken</c>、Line 需要 <c>ChannelAccessToken</c>。
/// 所有鍵值比對不區分大小寫。
/// </remarks>
public sealed class ChannelSettings
{
    /// <summary>
    /// 是否啟用此頻道；設為 <see langword="false"/> 時，系統將略過此頻道的訊息處理。
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// 頻道設定參數字典，鍵為參數名稱（不區分大小寫），值為對應的設定字串。
    /// </summary>
    /// <remarks>
    /// 常見的參數鍵包括 <c>BotToken</c>（Telegram）、<c>ChannelAccessToken</c>（Line）、
    /// <c>WebhookUrl</c>（各頻道）等。各頻道實作類別負責從此字典中提取所需的參數。
    /// </remarks>
    public Dictionary<string, string> Parameters { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
