namespace MessageHub.Core;

/// <summary>
/// 重試管線介面 — 抽象化重試邏輯，由 Infrastructure 提供實際實作（如 Polly）。
/// 對應 MESSAGE_BUS_ARCHITECTURE 規格：3 次指數退避重試。
/// </summary>
public interface IRetryPipeline
{
    /// <summary>
    /// 以重試策略執行指定的非同步動作。
    /// </summary>
    Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default);
}
