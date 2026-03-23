namespace MessageHub.Core.Models;

/// <summary>
/// 訊息方向列舉 — 表示訊息在系統中的流向類型。
/// </summary>
/// <remarks>
/// 用於 <see cref="MessageLogEntry.Direction"/> 欄位，以便日誌查詢時按方向篩選訊息。
/// </remarks>
public enum MessageDirection
{
    /// <summary>
    /// 入站 — 訊息由外部頻道（如 Telegram、Line）接收進入系統。
    /// </summary>
    Inbound,

    /// <summary>
    /// 出站 — 訊息由系統發送至外部頻道的目標對象。
    /// </summary>
    Outbound,

    /// <summary>
    /// 系統 — 由系統內部產生的事件或通知訊息，非來自外部頻道亦非發送至外部。
    /// </summary>
    System
}
