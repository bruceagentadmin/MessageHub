using System.Collections.Concurrent;
using MessageHub.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace MessageHub.Infrastructure;

/// <summary>
/// 渠道管理員 — 作為後台服務，負責監聽 Bus 上的訊息，並根據訊息指示分發到對應的實體渠道。
/// 包含 Polly 重試 (3 次指數退避)、Dead Letter Queue、Per-Channel 速率限制。
/// 對應 MESSAGE_BUS_ARCHITECTURE 規格文件中的 ChannelManager。
/// </summary>
public sealed class ChannelManager(
    IMessageBus messageBus,
    ChannelFactory channelFactory,
    IChannelSettingsService channelSettingsService,
    IMessageLogStore logStore,
    ILogger<ChannelManager> logger) : BackgroundService
{
    private static readonly ResiliencePipeline RetryPipeline = new ResiliencePipelineBuilder()
        .AddRetry(new RetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            Delay = TimeSpan.FromSeconds(1),
            BackoffType = DelayBackoffType.Exponential,
            ShouldHandle = new PredicateBuilder().Handle<Exception>()
        })
        .Build();

    private readonly ConcurrentDictionary<string, SemaphoreSlim> _rateLimiters = new(StringComparer.OrdinalIgnoreCase);

    private SemaphoreSlim GetRateLimiter(string channel)
        => _rateLimiters.GetOrAdd(channel, _ => new SemaphoreSlim(1, 1));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("ChannelManager 開始監聽 MessageBus...");

        await foreach (var message in messageBus.ConsumeOutboundAsync(stoppingToken))
        {
            var limiter = GetRateLimiter(message.Channel);
            await limiter.WaitAsync(stoppingToken);
            try
            {
                await ProcessMessageAsync(message, stoppingToken);
            }
            finally
            {
                limiter.Release();
            }
        }
    }

    private async Task ProcessMessageAsync(OutboundMessage message, CancellationToken stoppingToken)
    {
        try
        {
            var channel = channelFactory.GetChannel(message.Channel);

            var config = await channelSettingsService.GetAsync(stoppingToken);
            config.Channels.TryGetValue(message.Channel, out var settings);
            if (settings is not { Enabled: true })
            {
                throw new InvalidOperationException($"頻道 {message.Channel} 未啟用或不存在");
            }

            await RetryPipeline.ExecuteAsync(async ct =>
            {
                await channel.SendAsync(message.ChatId, message, settings, ct);
            }, stoppingToken);

            var successLog = new MessageLogEntry(
                Guid.NewGuid(),
                DateTimeOffset.UtcNow,
                message.TenantId,
                message.Channel,
                MessageDirection.Outbound,
                DeliveryStatus.Delivered,
                message.ChatId,
                message.Content,
                "ChannelManager",
                "發送成功");

            await logStore.AddAsync(successLog, stoppingToken);
            logger.LogInformation("訊息已發送至 {Channel} -> {ChatId}", message.Channel, message.ChatId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "發送訊息至 {Channel} 失敗（已重試 3 次），移至 Dead Letter Queue", message.Channel);

            var deadLetter = new DeadLetterMessage(message, ex.Message, 3, DateTimeOffset.UtcNow);
            await messageBus.PublishDeadLetterAsync(deadLetter, stoppingToken);

            var failedLog = new MessageLogEntry(
                Guid.NewGuid(),
                DateTimeOffset.UtcNow,
                message.TenantId,
                message.Channel,
                MessageDirection.Outbound,
                DeliveryStatus.Failed,
                message.ChatId,
                message.Content,
                "ChannelManager",
                $"重試 3 次後仍失敗：{ex.Message}");

            await logStore.AddAsync(failedLog, stoppingToken);
        }
    }
}
