using MessageHub.Core;
using MessageHub.Core.Models;
using MessageHub.Core.Services;

namespace MessageHub.Tests;

public class MessageOrchestratorTests
{
    [Fact]
    public async Task SendManualAsync_ShouldUseRecentTarget_WhenTargetIdIsEmpty()
    {
        var logStore = new InMemoryLogStore();
        var messageBus = new FakeMessageBus();
        var channel = new TestChannel("telegram");
        var factory = new ChannelFactory([channel]);
        var recentTargets = new TestRecentTargetStore(new RecentTargetInfo("telegram", "chat-123", "Bruce", DateTimeOffset.UtcNow));
        var processor = new UnifiedMessageProcessor(logStore, factory, recentTargets, messageBus);

        var result = await processor.SendManualAsync(new SendMessageRequest(
            "demo-tenant",
            "telegram",
            "",
            "hello",
            "test"));

        Assert.Equal(DeliveryStatus.Pending, result.Status);
        Assert.Equal("chat-123", result.TargetId);
        Assert.Equal("Bruce", result.TargetDisplayName);
        Assert.Null(channel.LastSentMessage);
        Assert.Single(messageBus.OutboundMessages);
        Assert.Equal("hello", messageBus.OutboundMessages[0].Content);
        Assert.Equal("chat-123", messageBus.OutboundMessages[0].ChatId);
    }

    [Fact]
    public async Task SendManualAsync_ShouldReturnFailedLog_WhenNoTargetExists()
    {
        var logStore = new InMemoryLogStore();
        var messageBus = new FakeMessageBus();
        var channel = new TestChannel("line");
        var factory = new ChannelFactory([channel]);
        var recentTargets = new TestRecentTargetStore(null);
        var processor = new UnifiedMessageProcessor(logStore, factory, recentTargets, messageBus);

        var result = await processor.SendManualAsync(new SendMessageRequest(
            "demo-tenant",
            "line",
            "",
            "hello",
            "test"));

        Assert.Equal(DeliveryStatus.Failed, result.Status);
        Assert.Equal("unknown", result.TargetId);
        Assert.Null(result.TargetDisplayName);
        Assert.Contains("找不到可用的 targetId", result.Details);
        Assert.Null(channel.LastSentMessage);
        Assert.Empty(messageBus.OutboundMessages);
    }

    [Fact]
    public async Task HandleInboundAsync_ShouldStoreRecentTarget_AndSendAutoReply()
    {
        var logStore = new InMemoryLogStore();
        var messageBus = new FakeMessageBus();
        var channel = new TestChannel("telegram");
        var factory = new ChannelFactory([channel]);
        var recentTargets = new TestRecentTargetStore(null);
        var processor = new UnifiedMessageProcessor(logStore, factory, recentTargets, messageBus);

        var result = await processor.HandleInboundAsync("tenant-a", "telegram", new WebhookTextMessageRequest("chat-777", "user-1", "hi"));
        var logs = await logStore.GetRecentAsync(10);

        Assert.Equal(DeliveryStatus.Delivered, result.Status);
        Assert.Single(messageBus.OutboundMessages);
        Assert.Equal("chat-777", messageBus.OutboundMessages[0].ChatId);
        Assert.Contains("已收到：hi", messageBus.OutboundMessages[0].Content);
        Assert.Single(logs);
        Assert.Equal("user-1", result.TargetDisplayName);
        Assert.Equal("chat-777", recentTargets.Stored?.TargetId);
    }
}

file sealed class FakeMessageBus : IMessageBus
{
    public List<OutboundMessage> OutboundMessages { get; } = [];

    public ValueTask PublishOutboundAsync(OutboundMessage message, CancellationToken cancellationToken = default)
    {
        OutboundMessages.Add(message);
        return ValueTask.CompletedTask;
    }

    public async IAsyncEnumerable<OutboundMessage> ConsumeOutboundAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        yield break;
    }

    public ValueTask PublishInboundAsync(InboundMessage message, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    public async IAsyncEnumerable<InboundMessage> ConsumeInboundAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        yield break;
    }

    public ValueTask PublishDeadLetterAsync(DeadLetterMessage message, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    public async IAsyncEnumerable<DeadLetterMessage> ConsumeDeadLetterAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        yield break;
    }
}

file sealed class InMemoryLogStore : IMessageLogStore
{
    private readonly List<MessageLogEntry> _items = [];

    public Task AddAsync(MessageLogEntry entry, CancellationToken cancellationToken = default)
    {
        _items.Add(entry);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<MessageLogEntry>> GetRecentAsync(int count, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<MessageLogEntry>>(_items.TakeLast(count).Reverse().ToList());
}

file sealed class TestChannel(string name) : IChannel
{
    public string Name => name;
    public OutboundMessage? LastSentMessage { get; private set; }

    public Task<InboundMessage> ParseRequestAsync(string tenantId, WebhookTextMessageRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(new InboundMessage(tenantId, Name, request.ChatId, request.SenderId, request.Content, DateTimeOffset.UtcNow));

    public Task SendAsync(string chatId, OutboundMessage message, ChannelSettings? settings = null, CancellationToken cancellationToken = default)
    {
        LastSentMessage = message;
        return Task.CompletedTask;
    }
}

file sealed class TestRecentTargetStore(RecentTargetInfo? initial) : IRecentTargetStore
{
    public RecentTargetInfo? Stored { get; private set; } = initial;

    public Task SetLastTargetAsync(string channel, string targetId, string? displayName = null, CancellationToken cancellationToken = default)
    {
        Stored = new RecentTargetInfo(channel, targetId, displayName, DateTimeOffset.UtcNow);
        return Task.CompletedTask;
    }

    public Task<RecentTargetInfo?> GetLastTargetAsync(string channel, CancellationToken cancellationToken = default)
        => Task.FromResult(Stored?.Channel.Equals(channel, StringComparison.OrdinalIgnoreCase) == true ? Stored : null);
}
