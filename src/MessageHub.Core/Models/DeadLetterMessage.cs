namespace MessageHub.Core.Models;

/// <summary>
/// 死信訊息 — 封裝發送失敗的 OutboundMessage 及其失敗資訊。
/// 對應 MESSAGE_BUS_ARCHITECTURE 規格文件中的 DeadLetterMessage。
/// </summary>
/// <remarks>
/// 當 <see cref="OutboundMessage"/> 經過 <c>IRetryPipeline</c> 重試仍失敗時，
/// 系統將其封裝為此記錄並寫入死信佇列，供後續人工排查或重新處理。
/// <see cref="RetryCount"/> 反映實際嘗試發送的總次數（含初次嘗試）。
/// </remarks>
/// <param name="Original">發送失敗的原始出站訊息，參見 <see cref="OutboundMessage"/>。</param>
/// <param name="Reason">失敗原因的描述字串，通常為例外訊息或錯誤碼說明。</param>
/// <param name="RetryCount">發送失敗前的總嘗試次數（含初次嘗試與所有重試）。</param>
/// <param name="FailedAt">最終確認失敗的時間戳記（含時區資訊）。</param>
public sealed record DeadLetterMessage(
    OutboundMessage Original,
    string Reason,
    int RetryCount,
    DateTimeOffset FailedAt);
