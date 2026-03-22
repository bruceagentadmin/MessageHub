using MessageHub.Core;
using Polly;
using Polly.Retry;

namespace MessageHub.Infrastructure;

/// <summary>
/// 基於 Polly 的重試管線 — 3 次指數退避重試。
/// 實作 Core 定義的 IRetryPipeline 介面。
/// </summary>
public sealed class PollyRetryPipeline : IRetryPipeline
{
    private static readonly ResiliencePipeline Pipeline = new ResiliencePipelineBuilder()
        .AddRetry(new RetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            Delay = TimeSpan.FromSeconds(1),
            BackoffType = DelayBackoffType.Exponential,
            ShouldHandle = new PredicateBuilder().Handle<Exception>()
        })
        .Build();

    public async Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
    {
        await Pipeline.ExecuteAsync(
            static (state, ct) => new ValueTask(state(ct)),
            action,
            cancellationToken);
    }
}
