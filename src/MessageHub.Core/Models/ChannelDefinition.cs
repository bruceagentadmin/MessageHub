namespace MessageHub.Core.Models;

/// <summary>
/// 頻道定義 — 描述系統中已註冊頻道的基本能力與說明，用於前端展示與頻道能力查詢。
/// </summary>
/// <remarks>
/// 由 <c>IChannel</c> 實作類別提供，並透過 <c>GET /api/control/channels</c> 端點回傳給前端。
/// <see cref="SupportsInbound"/> 與 <see cref="SupportsOutbound"/> 標示頻道支援的訊息方向，
/// 以便控制中心決定是否可對該頻道發起手動發送或接收操作。
/// </remarks>
/// <param name="Name">頻道的唯一識別名稱，例如 <c>telegram</c>、<c>line</c>。</param>
/// <param name="SupportsInbound">是否支援接收入站訊息（Webhook 接收方向）。</param>
/// <param name="SupportsOutbound">是否支援發送出站訊息（主動發送方向）。</param>
/// <param name="Description">頻道的人類可讀描述，用於 UI 呈現。</param>
public sealed record ChannelDefinition(
    string Name,
    bool SupportsInbound,
    bool SupportsOutbound,
    string Description);
