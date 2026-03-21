namespace MessageHub.Core;

/// <summary>
/// 死信訊息 — 封裝發送失敗的 OutboundMessage 及其失敗資訊。
/// 對應 MESSAGE_BUS_ARCHITECTURE 規格文件中的 DeadLetterMessage。
/// </summary>
public sealed record DeadLetterMessage(
    OutboundMessage Original,
    string Reason,
    int RetryCount,
    DateTimeOffset FailedAt);
