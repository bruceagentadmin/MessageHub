using System.Collections.Concurrent;
using MessageHub.Core.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MessageHub.Core.Bus;

/// <summary>
/// 渠道管理員 — 作為後台服務（<see cref="BackgroundService"/>），負責監聽 Bus 上的訊息，並根據訊息指示分發到對應的實體渠道。
/// 包含重試（透過 <see cref="IRetryPipeline"/>）、Dead Letter Queue、Per-Channel 速率限制。
/// 對應 MESSAGE_BUS_ARCHITECTURE 規格文件中的 ChannelManager。
/// <para>
/// 執行流程：
/// <list type="number">
///   <item><description>持續監聽 <see cref="IMessageBus"/> Outbound 佇列。</description></item>
///   <item><description>每則訊息透過 Per-Channel SemaphoreSlim 進行速率限制（同一頻道同時只處理一則）。</description></item>
///   <item><description>透過 <see cref="IRetryPipeline"/> 執行帶重試的發送，失敗後移入 DLQ。</description></item>
///   <item><description>發送結果（成功或失敗）皆記錄至 <see cref="IMessageLogStore"/>。</description></item>
/// </list>
/// </para>
/// </summary>
/// <param name="messageBus">訊息匯流排，用於消費 Outbound 佇列與發布至 DLQ。</param>
/// <param name="channelFactory">頻道工廠，用於取得對應頻道的 <see cref="IChannel"/> 實作。</param>
/// <param name="channelSettingsService">頻道設定服務，用於讀取發送前所需的頻道設定。</param>
/// <param name="logStore">訊息日誌儲存，記錄每則訊息的發送結果。</param>
/// <param name="retryPipeline">重試管線，封裝重試策略（指數退避等），由 Infrastructure 層提供。</param>
/// <param name="logger">結構化日誌記錄器。</param>
internal sealed class ChannelManager(
    IMessageBus messageBus,
    ChannelFactory channelFactory,
    IChannelSettingsService channelSettingsService,
    IMessageLogStore logStore,
    IRetryPipeline retryPipeline,
    ILogger<ChannelManager> logger) : BackgroundService
{
    // Per-Channel 速率限制字典：以頻道名稱（不分大小寫）為 Key，SemaphoreSlim 為 Value
    // 確保同一頻道的訊息不會並行發送，避免頻道 API 的速率限制問題
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _rateLimiters = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 取得（或建立）指定頻道的速率限制器 <see cref="SemaphoreSlim"/>。
    /// 使用 <see cref="ConcurrentDictionary{TKey, TValue}.GetOrAdd"/> 確保同一頻道只建立一個限制器。
    /// </summary>
    /// <param name="channel">頻道名稱（不分大小寫）。</param>
    /// <returns>對應頻道的 <see cref="SemaphoreSlim"/>，初始計數為 1（互斥鎖語意）。</returns>
    private SemaphoreSlim GetRateLimiter(string channel)
        // GetOrAdd 是執行緒安全的；若 Key 不存在，建立計數為 1 的 Semaphore 作為互斥鎖
        => _rateLimiters.GetOrAdd(channel, _ => new SemaphoreSlim(1, 1));

    /// <summary>
    /// 背景服務主迴圈 — 持續監聽 Outbound 佇列並逐一處理訊息。
    /// 在應用程式關閉時由 <paramref name="stoppingToken"/> 取消，迴圈自然結束。
    /// </summary>
    /// <param name="stoppingToken">由宿主（Host）在應用程式停止時觸發的取消權杖。</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("ChannelManager 開始監聽 MessageBus...");

        // ConsumeOutboundAsync 回傳 IAsyncEnumerable，會持續阻塞等待新訊息
        // stoppingToken 取消時，foreach 迴圈會自動結束
        await foreach (var message in messageBus.ConsumeOutboundAsync(stoppingToken))
        {
            // 取得該頻道的速率限制器並等待進入（確保同頻道串行處理）
            var limiter = GetRateLimiter(message.Channel);
            await limiter.WaitAsync(stoppingToken);
            try
            {
                // 在速率限制器保護下處理訊息，確保同一頻道不會並行呼叫 API
                await ProcessMessageAsync(message, stoppingToken);
            }
            finally
            {
                // 無論成功或失敗，必須釋放 Semaphore，否則該頻道將永久阻塞
                limiter.Release();
            }
        }
    }

    /// <summary>
    /// 從訊息的 Metadata 物件中，以反射方式提取 <c>TargetDisplayName</c> 屬性值。
    /// 用於記錄日誌時附加人類可讀的目標名稱（例如使用者暱稱）。
    /// </summary>
    /// <param name="metadata">訊息的可選 Metadata 物件，型別不固定（使用 <see langword="object"/>）。</param>
    /// <returns>
    /// 若 <paramref name="metadata"/> 包含非空白的 <c>TargetDisplayName</c> 屬性，回傳其字串值；
    /// 否則回傳 <see langword="null"/>。
    /// </returns>
    private static string? ExtractTargetDisplayName(object? metadata)
    {
        if (metadata is null)
        {
            return null;
        }

        // 使用反射動態讀取 TargetDisplayName 屬性，因為 Metadata 型別在編譯期不確定
        // 這是刻意的設計：允許不同頻道的 Metadata 攜帶不同欄位，保持彈性
        var property = metadata.GetType().GetProperty("TargetDisplayName");
        var value = property?.GetValue(metadata)?.ToString();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    /// <summary>
    /// 處理單一出站訊息：取得頻道實例、驗證設定、透過重試管線發送，並記錄結果日誌。
    /// 若發送失敗（重試耗盡），將訊息移入 Dead Letter Queue 並記錄失敗日誌。
    /// </summary>
    /// <param name="message">要發送的出站訊息。</param>
    /// <param name="stoppingToken">用於取消非同步操作的取消權杖。</param>
    private async Task ProcessMessageAsync(OutboundMessage message, CancellationToken stoppingToken)
    {
        try
        {
            // 透過 ChannelFactory 取得對應頻道的 IChannel 實作
            // 若找不到，GetChannel 會拋出例外，訊息將進入 catch 分支
            var channel = channelFactory.GetChannel(message.Channel);

            // 讀取頻道設定，並確認頻道已啟用；停用頻道不應繼續發送
            var config = await channelSettingsService.GetAsync(stoppingToken);
            var settings = ChannelSettingsResolver.FindSettings(config, message.Channel);
            if (settings is not { Enabled: true })
            {
                throw new InvalidOperationException($"頻道 {message.Channel} 未啟用或不存在");
            }

            // 透過 IRetryPipeline 執行發送，內部實作（例如 Polly）負責重試邏輯（指數退避）
            // 將 settings 預先傳入，避免每次重試都重新讀取設定（減少 I/O）
            await retryPipeline.ExecuteAsync(async ct =>
            {
                await channel.SendAsync(message.ChatId, message, settings, ct);
            }, stoppingToken);

            // 發送成功：建立成功日誌並存入日誌儲存
            var successLog = new MessageLogEntry(
                Guid.NewGuid(),
                DateTimeOffset.UtcNow,
                message.TenantId,
                message.Channel,
                MessageDirection.Outbound,
                DeliveryStatus.Delivered,
                message.ChatId,
                ExtractTargetDisplayName(message.Metadata),
                message.Content,
                "ChannelManager",
                "發送成功");

            await logStore.AddAsync(successLog, stoppingToken);
            logger.LogInformation("訊息已發送至 {Channel} -> {ChatId}", message.Channel, message.ChatId);
        }
        catch (Exception ex)
        {
            // 重試耗盡後仍失敗：記錄錯誤、移入 DLQ、建立失敗日誌
            logger.LogError(ex, "發送訊息至 {Channel} 失敗（已重試），移至 Dead Letter Queue", message.Channel);

            // 將失敗訊息封裝為 DeadLetterMessage，紀錄錯誤原因與重試次數（固定為 3）
            // 放入 DLQ 後可由監控儀表板顯示，供人工介入或後續補發處理
            var deadLetter = new DeadLetterMessage(message, ex.Message, 3, DateTimeOffset.UtcNow);
            await messageBus.PublishDeadLetterAsync(deadLetter, stoppingToken);

            // 建立失敗日誌並存入日誌儲存，讓日誌查詢 API 可以顯示失敗狀態
            var failedLog = new MessageLogEntry(
                Guid.NewGuid(),
                DateTimeOffset.UtcNow,
                message.TenantId,
                message.Channel,
                MessageDirection.Outbound,
                DeliveryStatus.Failed,
                message.ChatId,
                ExtractTargetDisplayName(message.Metadata),
                message.Content,
                "ChannelManager",
                $"重試後仍失敗：{ex.Message}");

            await logStore.AddAsync(failedLog, stoppingToken);
        }
    }
}
