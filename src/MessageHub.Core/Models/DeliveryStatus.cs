namespace MessageHub.Core.Models;

/// <summary>
/// 訊息投遞狀態列舉 — 表示單一訊息的當前投遞生命週期狀態。
/// </summary>
/// <remarks>
/// 用於 <see cref="MessageLogEntry.Status"/> 欄位，追蹤訊息從建立到最終結果的狀態流轉。
/// </remarks>
public enum DeliveryStatus
{
    /// <summary>
    /// 等待中 — 訊息已進入佇列，尚未嘗試發送。
    /// </summary>
    Pending,

    /// <summary>
    /// 已投遞 — 訊息已成功發送至目標頻道。
    /// </summary>
    Delivered,

    /// <summary>
    /// 失敗 — 訊息發送失敗，包含重試耗盡後仍無法投遞的情況。
    /// </summary>
    Failed
}
