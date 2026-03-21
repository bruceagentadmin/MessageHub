# Spec Compliance Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Make the MessageHub solution strictly comply with both spec documents (CHANNEL_SYSTEM_CS_MULTITENANT.md & MESSAGE_BUS_ARCHITECTURE_CS.md) — fix all 6 audit gaps in priority order.

**Architecture:** Refactor webhook controllers to use the full pipeline (ChannelFactory → ParseRequest → ProcessAsync → PublishOutbound), add Inbound queue to IMessageBus, add Polly retry/DLQ/rate-limiting in ChannelManager, unify all sends through MessageBus, convert ChannelConfig.Channels to Dictionary, and rename methods/fields to match spec exactly.

**Tech Stack:** .NET 8, System.Threading.Channels, Polly (new dependency), xUnit

**Baseline:** 15/15 tests passing. Build clean. Every change MUST preserve this.

---

## Priority 1: Webhook Controller 改走完整 Pipeline

### Task 1: Refactor TelegramWebhookController to use UnifiedMessageProcessor

**Files:**
- Modify: `src/MessageHub.Api/Controllers/TelegramWebhookController.cs`

**Step 1: Rewrite TelegramWebhookController**

Replace the entire controller to inject `UnifiedMessageProcessor` and delegate to `HandleInboundAsync`. The controller should:
1. Accept `JsonElement` payload (unchanged route: `api/telegram/webhook`)
2. Extract chatId, text, displayName from the Telegram update JSON
3. Build a `WebhookTextMessageRequest(chatId, senderId, content)`
4. Call `processor.HandleInboundAsync("telegram-default", "telegram", request, ct)`
5. Return `Ok()` even if processing fails (spec says return HTTP 200 immediately)

```csharp
using System.Text.Json;
using MessageHub.Application;
using MessageHub.Core;
using Microsoft.AspNetCore.Mvc;

namespace MessageHub.Api.Controllers;

[ApiController]
[Route("api/telegram")]
public sealed class TelegramWebhookController(UnifiedMessageProcessor processor, ILogger<TelegramWebhookController> logger) : ControllerBase
{
    [HttpPost("webhook")]
    public async Task<IActionResult> Handle([FromBody] JsonElement data, CancellationToken cancellationToken)
    {
        string? chatId = null;
        string? text = null;
        string? displayName = null;

        try
        {
            if (data.TryGetProperty("message", out var message))
            {
                if (message.TryGetProperty("chat", out var chat) && chat.TryGetProperty("id", out var chatIdElement))
                    chatId = chatIdElement.ToString();

                if (message.TryGetProperty("text", out var textElement))
                    text = textElement.GetString();

                if (message.TryGetProperty("from", out var from))
                {
                    var firstName = from.TryGetProperty("first_name", out var fn) ? fn.GetString() : string.Empty;
                    var lastName = from.TryGetProperty("last_name", out var ln) ? ln.GetString() : string.Empty;
                    displayName = $"{firstName} {lastName}".Trim();
                }
            }
        }
        catch
        {
            // Non-message events (e.g. bot added to group) — ignore parse failures
        }

        if (string.IsNullOrWhiteSpace(chatId) || string.IsNullOrWhiteSpace(text))
        {
            return Ok(); // Non-text events, return 200 per spec
        }

        try
        {
            var request = new WebhookTextMessageRequest(chatId, displayName ?? chatId, text);
            await processor.HandleInboundAsync("telegram-default", "telegram", request, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Telegram webhook 處理失敗");
        }

        return Ok();
    }
}
```

**Step 2: Build and verify**

Run: `dotnet build`
Expected: 0 errors

**Step 3: Run tests**

Run: `dotnet test --verbosity minimal`
Expected: 15/15 pass (controller is not directly tested, existing tests unaffected)

### Task 2: Refactor LineWebhookController to use UnifiedMessageProcessor

**Files:**
- Modify: `src/MessageHub.Api/Controllers/LineWebhookController.cs`

**Step 1: Rewrite LineWebhookController**

Same pattern as Telegram. Extract userId, text from Line events JSON. Delegate to `processor.HandleInboundAsync`.

```csharp
using System.Text.Json;
using MessageHub.Application;
using MessageHub.Core;
using Microsoft.AspNetCore.Mvc;

namespace MessageHub.Api.Controllers;

[ApiController]
[Route("api/line")]
public sealed class LineWebhookController(UnifiedMessageProcessor processor, ILogger<LineWebhookController> logger) : ControllerBase
{
    [HttpPost("webhook")]
    public async Task<IActionResult> Handle([FromBody] JsonElement data, CancellationToken cancellationToken)
    {
        string? userId = null;
        string? text = null;

        try
        {
            if (data.TryGetProperty("events", out var events) && events.ValueKind == JsonValueKind.Array && events.GetArrayLength() > 0)
            {
                var firstEvent = events[0];

                if (firstEvent.TryGetProperty("source", out var source) && source.TryGetProperty("userId", out var userIdElement))
                    userId = userIdElement.GetString();

                if (firstEvent.TryGetProperty("message", out var message) && message.TryGetProperty("text", out var textElement))
                    text = textElement.GetString();
            }
        }
        catch
        {
            // Non-text events — ignore parse failures
        }

        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(text))
        {
            return Ok(); // Non-text events, return 200 per spec
        }

        try
        {
            var request = new WebhookTextMessageRequest(userId, userId, text);
            await processor.HandleInboundAsync("line-default", "line", request, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Line webhook 處理失敗");
        }

        return Ok();
    }
}
```

**Step 2: Build and run tests**

Run: `dotnet build && dotnet test --verbosity minimal`
Expected: Build clean, 15/15 pass

### Task 3: Modify UnifiedMessageProcessor to publish reply via MessageBus instead of direct send

**Files:**
- Modify: `src/MessageHub.Application/UnifiedMessageProcessor.cs`
- Modify: `src/MessageHub.Application/MessageHub.Application.csproj` (no change needed — IMessageBus is in Core)

**Step 1: Add IMessageBus dependency and change HandleInboundAsync**

The HandleInboundAsync should:
1. Parse request via channel (unchanged)
2. Store recent target (unchanged)
3. Log inbound (unchanged)
4. Call ProcessAsync to get reply text (unchanged)
5. **Publish reply to MessageBus** instead of calling `client.SendAsync` directly
6. Return the inbound log instead (since actual send is now async via ChannelManager)

Also change SendManualAsync to publish via MessageBus.

```csharp
using MessageHub.Core;

namespace MessageHub.Application;

public sealed class UnifiedMessageProcessor(
    IMessageLogStore logStore,
    ChannelFactory channelFactory,
    IRecentTargetStore recentTargetStore,
    IMessageBus messageBus) : IMessageProcessor
{
    public Task<string> ProcessAsync(InboundMessage message, CancellationToken cancellationToken = default)
    {
        return Task.FromResult($"[POC 回覆] 已收到：{message.Content}");
    }

    public async Task<MessageLogEntry> HandleInboundAsync(string tenantId, string channel, WebhookTextMessageRequest request, CancellationToken cancellationToken = default)
    {
        var client = channelFactory.GetChannel(channel);
        var inbound = await client.ParseRequestAsync(tenantId, request, cancellationToken);

        await recentTargetStore.SetLastTargetAsync(inbound.Channel, inbound.ChatId, inbound.SenderId, cancellationToken);

        var inboundLog = new MessageLogEntry(
            Guid.NewGuid(),
            inbound.ReceivedAt,
            inbound.TenantId,
            inbound.Channel,
            MessageDirection.Inbound,
            DeliveryStatus.Delivered,
            inbound.ChatId,
            inbound.Content,
            $"{inbound.Channel} webhook",
            $"Sender={inbound.SenderId}");

        await logStore.AddAsync(inboundLog, cancellationToken);

        var replyText = await ProcessAsync(inbound, cancellationToken);

        var reply = new OutboundMessage(
            inbound.TenantId,
            inbound.Channel,
            inbound.ChatId,
            replyText,
            DateTimeOffset.UtcNow,
            "AutoReply");

        await messageBus.PublishOutboundAsync(reply, cancellationToken);

        return inboundLog;
    }

    public async Task<MessageLogEntry> SendManualAsync(SendMessageRequest request, CancellationToken cancellationToken = default)
    {
        _ = channelFactory.GetChannel(request.Channel); // validate channel exists

        var targetId = request.TargetId;

        if (string.IsNullOrWhiteSpace(targetId))
        {
            var recent = await recentTargetStore.GetLastTargetAsync(request.Channel, cancellationToken);
            targetId = recent?.TargetId;
        }

        if (string.IsNullOrWhiteSpace(targetId))
        {
            var failedLog = new MessageLogEntry(
                Guid.NewGuid(),
                DateTimeOffset.UtcNow,
                request.TenantId,
                request.Channel,
                MessageDirection.Outbound,
                DeliveryStatus.Failed,
                "unknown",
                request.Content,
                "control center",
                "找不到可用的 targetId，且該頻道沒有最近互動對象");
            await logStore.AddAsync(failedLog, cancellationToken);
            return failedLog;
        }

        var outbound = new OutboundMessage(
            request.TenantId,
            request.Channel,
            targetId,
            request.Content,
            DateTimeOffset.UtcNow,
            request.TriggeredBy ?? "ControlCenter");

        await messageBus.PublishOutboundAsync(outbound, cancellationToken);

        var log = new MessageLogEntry(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            request.TenantId,
            request.Channel,
            MessageDirection.Outbound,
            DeliveryStatus.Pending,
            targetId,
            request.Content,
            request.TriggeredBy ?? "ControlCenter",
            "已發佈至 MessageBus，等待 ChannelManager 發送");
        await logStore.AddAsync(log, cancellationToken);
        return log;
    }

    public Task<IReadOnlyList<MessageLogEntry>> GetRecentLogsAsync(int count, CancellationToken cancellationToken = default)
        => logStore.GetRecentAsync(count, cancellationToken);

    public IReadOnlyList<ChannelDefinition> GetChannels()
        => channelFactory.GetDefinitions();
}
```

**Step 2: Add DeliveryStatus.Pending if not exists**

Check `src/MessageHub.Core/Models/DeliveryStatus.cs`. If `Pending` is missing, add it.

**Step 3: Update tests for new constructor and behavior**

Files to update:
- `tests/MessageHub.Tests/MessageOrchestratorTests.cs` — add IMessageBus mock, update assertions
- `tests/MessageHub.Tests/ControlCenterControllerTests.cs` — add IMessageBus mock to processor constructor

The tests need:
1. A `FakeMessageBus` that captures published messages
2. HandleInboundAsync now returns inbound log (not reply log), and the actual send goes through Bus
3. SendManualAsync now returns Pending log, not Delivered log

**Step 4: Build and run tests**

Run: `dotnet build && dotnet test --verbosity minimal`
Expected: All tests pass (after test updates)

---

## Priority 2: Add Inbound Queue to IMessageBus

### Task 4: Add Inbound queue API to IMessageBus

**Files:**
- Modify: `src/MessageHub.Core/IMessageBus.cs`

**Step 1: Add PublishInboundAsync and ConsumeInboundAsync**

```csharp
namespace MessageHub.Core;

public interface IMessageBus
{
    // Outbound
    ValueTask PublishOutboundAsync(OutboundMessage message, CancellationToken cancellationToken = default);
    IAsyncEnumerable<OutboundMessage> ConsumeOutboundAsync(CancellationToken cancellationToken);

    // Inbound
    ValueTask PublishInboundAsync(InboundMessage message, CancellationToken cancellationToken = default);
    IAsyncEnumerable<InboundMessage> ConsumeInboundAsync(CancellationToken cancellationToken);
}
```

### Task 5: Implement Inbound queue in MessageBus

**Files:**
- Modify: `src/MessageHub.Infrastructure/MessageBus.cs`

**Step 1: Add inbound channel**

```csharp
using System.Threading.Channels;
using MessageHub.Core;

namespace MessageHub.Infrastructure;

public sealed class MessageBus : IMessageBus
{
    private readonly Channel<OutboundMessage> _outbound = Channel.CreateUnbounded<OutboundMessage>(
        new UnboundedChannelOptions { SingleReader = false, SingleWriter = false });

    private readonly Channel<InboundMessage> _inbound = Channel.CreateUnbounded<InboundMessage>(
        new UnboundedChannelOptions { SingleReader = false, SingleWriter = false });

    // Outbound
    public ValueTask PublishOutboundAsync(OutboundMessage message, CancellationToken cancellationToken = default)
        => _outbound.Writer.WriteAsync(message, cancellationToken);

    public async IAsyncEnumerable<OutboundMessage> ConsumeOutboundAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var message in _outbound.Reader.ReadAllAsync(cancellationToken))
            yield return message;
    }

    // Inbound
    public ValueTask PublishInboundAsync(InboundMessage message, CancellationToken cancellationToken = default)
        => _inbound.Writer.WriteAsync(message, cancellationToken);

    public async IAsyncEnumerable<InboundMessage> ConsumeInboundAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var message in _inbound.Reader.ReadAllAsync(cancellationToken))
            yield return message;
    }

    public int OutboundPendingCount => _outbound.Reader.Count;
    public int InboundPendingCount => _inbound.Reader.Count;
}
```

**Step 2: Build and run tests**

Run: `dotnet build && dotnet test --verbosity minimal`
Expected: Build clean, all tests pass

---

## Priority 3: Add Resilience (Retry, DLQ, Rate Limiting)

### Task 6: Add Polly NuGet package to Infrastructure

**Files:**
- Modify: `src/MessageHub.Infrastructure/MessageHub.Infrastructure.csproj`

**Step 1: Add Polly package**

Run: `dotnet add src/MessageHub.Infrastructure/MessageHub.Infrastructure.csproj package Microsoft.Extensions.Http.Polly`

Or add to csproj:
```xml
<PackageReference Include="Polly" Version="8.5.2" />
```

**Step 2: Build**

Run: `dotnet build`
Expected: 0 errors

### Task 7: Add Dead Letter Queue to IMessageBus and MessageBus

**Files:**
- Modify: `src/MessageHub.Core/IMessageBus.cs`
- Modify: `src/MessageHub.Infrastructure/MessageBus.cs`

**Step 1: Add DLQ API to IMessageBus**

Add to IMessageBus:
```csharp
// Dead Letter Queue
ValueTask PublishDeadLetterAsync(OutboundMessage message, string reason, CancellationToken cancellationToken = default);
```

Note: We need a DeadLetterMessage wrapper. Add a new model:
```csharp
// src/MessageHub.Core/Models/DeadLetterMessage.cs
namespace MessageHub.Core;

public sealed record DeadLetterMessage(
    OutboundMessage Original,
    string Reason,
    int RetryCount,
    DateTimeOffset FailedAt);
```

Update IMessageBus:
```csharp
ValueTask PublishDeadLetterAsync(DeadLetterMessage message, CancellationToken cancellationToken = default);
IAsyncEnumerable<DeadLetterMessage> ConsumeDeadLetterAsync(CancellationToken cancellationToken);
```

**Step 2: Implement in MessageBus**

Add a third channel for DLQ.

**Step 3: Build and run tests**

### Task 8: Add retry logic with Polly to ChannelManager

**Files:**
- Modify: `src/MessageHub.Infrastructure/ChannelManager.cs`

**Step 1: Add Polly retry pipeline**

ChannelManager should:
1. On each message, attempt SendAsync with retry (3 retries, exponential backoff)
2. If all retries fail, publish to Dead Letter Queue
3. Log each retry attempt

```csharp
using MessageHub.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace MessageHub.Infrastructure;

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
            ShouldHandle = new PredicateBuilder().Handle<HttpRequestException>()
        })
        .Build();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("ChannelManager 開始監聽 MessageBus...");

        await foreach (var message in messageBus.ConsumeOutboundAsync(stoppingToken))
        {
            try
            {
                var channel = channelFactory.GetChannel(message.Channel);

                // Load settings for this channel
                var config = await channelSettingsService.GetAsync(stoppingToken);
                var settings = config.Channels.FirstOrDefault(
                    x => x.Enabled && x.Type.Equals(message.Channel, StringComparison.OrdinalIgnoreCase));

                await RetryPipeline.ExecuteAsync(async ct =>
                {
                    var log = await channel.SendAsync(message.TargetId, message, settings, ct);
                    await logStore.AddAsync(log, ct);
                    logger.LogInformation("訊息已發送至 {Channel} -> {TargetId}", message.Channel, message.TargetId);
                }, stoppingToken);
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
                    message.TargetId,
                    message.Content,
                    "ChannelManager",
                    $"重試 3 次後仍失敗：{ex.Message}");

                await logStore.AddAsync(failedLog, stoppingToken);
            }
        }
    }
}
```

**Step 2: Build and run tests**

Run: `dotnet build && dotnet test --verbosity minimal`
Expected: All pass

### Task 9: Add per-channel rate limiting to ChannelManager

**Files:**
- Modify: `src/MessageHub.Infrastructure/ChannelManager.cs`

**Step 1: Add SemaphoreSlim per channel**

Add a `ConcurrentDictionary<string, SemaphoreSlim>` to limit concurrent sends per channel. Default to 1 concurrent send per channel (can be made configurable later).

```csharp
private readonly ConcurrentDictionary<string, SemaphoreSlim> _rateLimiters = new(StringComparer.OrdinalIgnoreCase);

private SemaphoreSlim GetRateLimiter(string channel)
    => _rateLimiters.GetOrAdd(channel, _ => new SemaphoreSlim(1, 1));
```

In ExecuteAsync, wrap the send with:
```csharp
var limiter = GetRateLimiter(message.Channel);
await limiter.WaitAsync(stoppingToken);
try { /* retry + send */ }
finally { limiter.Release(); }
```

**Step 2: Build and run tests**

---

## Priority 4: Unify all sends through MessageBus

(Already done in Task 3 — HandleInboundAsync and SendManualAsync now use PublishOutboundAsync. NotificationService already uses it. This priority is complete after Task 3.)

---

## Priority 5: ChannelConfig.Channels 改為 Dictionary

### Task 10: Change ChannelConfig.Channels from List to Dictionary

**Files:**
- Modify: `src/MessageHub.Core/Models/ChannelConfig.cs`
- Modify: `src/MessageHub.Core/Models/ChannelSettings.cs` (remove Id, keep Type)
- Modify: `src/MessageHub.Application/ChannelSettingsService.cs`
- Modify: `src/MessageHub.Infrastructure/NotificationService.cs`
- Modify: `src/MessageHub.Infrastructure/TelegramChannel.cs`
- Modify: `src/MessageHub.Infrastructure/LineChannel.cs`
- Modify: `src/MessageHub.Infrastructure/EmailChannel.cs`
- Modify: `src/MessageHub.Infrastructure/JsonChannelSettingsStore.cs`
- Modify: `src/MessageHub.Api/Controllers/ControlCenterController.cs`
- Modify: Multiple test files

**Step 1: Update ChannelConfig**

Spec JSON shows: `{ "channels": { "line": { "enabled": true, "parameters": {...} }, "telegram": {...} } }`

The key IS the channel name. ChannelSettings no longer needs Id or Type (the key serves as the identifier).

```csharp
// ChannelConfig.cs
namespace MessageHub.Core;

public sealed class ChannelConfig
{
    public Guid TenantId { get; set; }
    public Dictionary<string, ChannelSettings> Channels { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
```

```csharp
// ChannelSettings.cs
namespace MessageHub.Core;

public sealed class ChannelSettings
{
    public bool Enabled { get; set; }
    public Dictionary<string, string> Parameters { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
```

**Step 2: Update all consumers**

Every place that does `config.Channels.FirstOrDefault(x => x.Type.Equals(...))` changes to `config.Channels.TryGetValue(channelName, out var settings)`.

Update ChannelSettingsService: NormalizeConfig iterates dictionary entries. Remove Id/Type based filtering. Normalize by key.

Update NotificationService: `config.Channels.TryGetValue(channelName, out var settings)` and check `settings.Enabled`.

Update Channel implementations (TelegramChannel, LineChannel, EmailChannel): GetSettingsAsync changes to dictionary lookup by Name.

Update tests: All test data uses `Dictionary<string, ChannelSettings>` instead of `List<ChannelSettings>`.

**Step 3: Build and run tests after each file change**

Run: `dotnet build && dotnet test --verbosity minimal`
Expected: All pass after all updates complete

---

## Priority 6: Rename methods and fields to match spec exactly

### Task 11: Rename INotificationService method

**Files:**
- Modify: `src/MessageHub.Core/INotificationService.cs`
- Modify: `src/MessageHub.Infrastructure/NotificationService.cs`
- Modify all callers (if any)

**Step 1: Rename SendGlobalNotificationAsync → SendNotificationAsync**

Spec says: `SendNotificationAsync(tenantId, channel, msg) Task`

### Task 12: Rename OutboundMessage fields

**Files:**
- Modify: `src/MessageHub.Core/Models/OutboundMessage.cs`
- Modify all consumers

**Step 1: Rename TargetId → ChatId, add Metadata**

Spec says: `Channel, ChatId, Content, Metadata: object`

```csharp
public sealed record OutboundMessage(
    string TenantId,
    string Channel,
    string ChatId,
    string Content,
    object? Metadata = null);
```

Note: This removes CreatedAt and TriggeredBy. Move them to Metadata if needed, or keep them as additional fields. Spec shows `Metadata: object` as catch-all.

Decision: Keep TenantId (spec Tips recommend it). Add ChatId (rename from TargetId). Keep Content. Add Metadata. Drop CreatedAt and TriggeredBy as separate fields — pack into Metadata or keep as additional fields for backward compat.

Pragmatic choice (keep compat): 
```csharp
public sealed record OutboundMessage(
    string TenantId,
    string Channel,
    string ChatId,
    string Content,
    object? Metadata = null);
```

This requires updating all consumers that reference `.TargetId` → `.ChatId` and removing `.CreatedAt` / `.TriggeredBy` references.

### Task 13: Update IChannel.SendAsync return type

**Files:**
- Modify: `src/MessageHub.Core/IChannel.cs`
- Modify: All channel implementations
- Modify: All callers

**Step 1: Change return to Task (spec says Task, not Task<MessageLogEntry>)**

Spec: `SendAsync(chatId, message, settings) Task`

The logging responsibility moves to ChannelManager (already does logging). Channels just send.

```csharp
public interface IChannel
{
    string Name { get; }
    Task<InboundMessage> ParseRequestAsync(string tenantId, WebhookTextMessageRequest request, CancellationToken cancellationToken = default);
    Task SendAsync(string chatId, OutboundMessage message, ChannelSettings? settings = null, CancellationToken cancellationToken = default);
}
```

Update TelegramChannel, LineChannel, EmailChannel to return Task instead of Task<MessageLogEntry>. Move log creation to ChannelManager.

### Task 14: Final build and test verification

**Step 1: Run full build**

Run: `dotnet build`
Expected: 0 errors, 0 warnings

**Step 2: Run all tests**

Run: `dotnet test --verbosity minimal`
Expected: All tests pass (15 original + any new tests)

**Step 3: Verify spec compliance checklist**

- [ ] Webhook controllers use full pipeline (ChannelFactory → Parse → Process → PublishOutbound)
- [ ] IMessageBus has Inbound + Outbound queues
- [ ] Polly retry in ChannelManager
- [ ] Dead Letter Queue for failed messages
- [ ] Per-channel rate limiting (SemaphoreSlim)
- [ ] All sends go through MessageBus
- [ ] ChannelConfig.Channels is Dictionary<string, ChannelSettings>
- [ ] INotificationService.SendNotificationAsync (not SendGlobalNotificationAsync)
- [ ] OutboundMessage uses ChatId (not TargetId), has Metadata
- [ ] IChannel.SendAsync returns Task (not Task<MessageLogEntry>)
