namespace MessageHub.Core;

/// <summary>
/// 重試管線介面 — 抽象化重試邏輯，由 Infrastructure 層提供實際實作（如 Polly）。
/// 對應 MESSAGE_BUS_ARCHITECTURE 規格：3 次指數退避重試。
/// </summary>
public interface IRetryPipeline
{
    /// <summary>
    /// 以重試策略執行指定的非同步動作。
    /// 失敗時將依設定的退避策略自動重試，達到最大次數後拋出例外。
    /// </summary>
    /// <param name="action">
    /// 要執行的非同步委派方法，接收 <see cref="CancellationToken"/> 以支援取消。
    /// </param>
    /// <param name="cancellationToken">用於取消整個重試流程的權杖。</param>
    /// <exception cref="Exception">
    /// 當所有重試次數耗盡後，將拋出最後一次執行所產生的例外。
    /// </exception>
    Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default);
}
