using MessageHub.Application;
using MessageHub.Domain;

namespace MessageHub.Tests;

public class MessageOrchestratorTests
{
    [Fact]
    public async Task SendManualAsync_ShouldUseRecentTarget_WhenTargetIdIsEmpty()
    {
        var logStore = new InMemoryLogStore();
        var channel = new TestChannelClient("telegram");
        var registry = new TestChannelRegistry(channel);
        var recentTargets = new TestRecentTargetStore(new RecentTargetInfo("telegram", "chat-123", "Bruce", DateTimeOffset.UtcNow));
        var orchestrator = new MessageOrchestrator(logStore, registry, recentTargets);

        var result = await orchestrator.SendManualAsync(new SendMessageRequest(
            "demo-tenant",
            "telegram",
            "",
            "hello",
            "test"));

        Assert.Equal(DeliveryStatus.Delivered, result.Status);
        Assert.Equal("chat-123", result.TargetId);
        Assert.Equal("hello", channel.LastSentMessage?.Content);
        Assert.Equal("chat-123", channel.LastSentMessage?.TargetId);
    }

    [Fact]
    public async Task SendManualAsync_ShouldReturnFailedLog_WhenNoTargetExists()
    {
        var logStore = new InMemoryLogStore();
        var channel = new TestChannelClient("line");
        var registry = new TestChannelRegistry(channel);
        var recentTargets = new TestRecentTargetStore(null);
        var orchestrator = new MessageOrchestrator(logStore, registry, recentTargets);

        var result = await orchestrator.SendManualAsync(new SendMessageRequest(
            "demo-tenant",
            "line",
            "",
            "hello",
            "test"));

        Assert.Equal(DeliveryStatus.Failed, result.Status);
        Assert.Equal("unknown", result.TargetId);
        Assert.Contains("找不到可用的 targetId", result.Details);
        Assert.Null(channel.LastSentMessage);
    }

    [Fact]
    public async Task HandleInboundAsync_ShouldStoreRecentTarget_AndSendAutoReply()
    {
        var logStore = new InMemoryLogStore();
        var channel = new TestChannelClient("telegram");
        var registry = new TestChannelRegistry(channel);
        var recentTargets = new TestRecentTargetStore(null);
        var orchestrator = new MessageOrchestrator(logStore, registry, recentTargets);

        var result = await orchestrator.HandleInboundAsync("tenant-a", "telegram", new WebhookTextMessageRequest("chat-777", "user-1", "hi"));
        var logs = await logStore.GetRecentAsync(10);

        Assert.Equal(DeliveryStatus.Delivered, result.Status);
        Assert.Equal("chat-777", channel.LastSentMessage?.TargetId);
        Assert.Contains("已收到：hi", channel.LastSentMessage?.Content);
        Assert.Equal(2, logs.Count);
        Assert.Equal("chat-777", recentTargets.Stored?.TargetId);
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

file sealed class TestChannelClient(string name) : IChannelClient
{
    public string Name => name;
    public OutboundMessage? LastSentMessage { get; private set; }

    public Task<InboundMessage> ParseAsync(string tenantId, WebhookTextMessageRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(new InboundMessage(tenantId, Name, request.ChatId, request.SenderId, request.Content, DateTimeOffset.UtcNow));

    public Task<MessageLogEntry> SendAsync(OutboundMessage message, CancellationToken cancellationToken = default)
    {
        LastSentMessage = message;
        return Task.FromResult(new MessageLogEntry(Guid.NewGuid(), DateTimeOffset.UtcNow, message.TenantId, Name, MessageDirection.Outbound, DeliveryStatus.Delivered, message.TargetId, message.Content, $"{Name} test sender"));
    }
}

file sealed class TestChannelRegistry(params IChannelClient[] channels) : IChannelRegistry
{
    private readonly Dictionary<string, IChannelClient> _map = channels.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<ChannelDefinition> GetDefinitions()
        => _map.Values.Select(x => new ChannelDefinition(x.Name, true, true, x.Name)).ToList();

    public IChannelClient Get(string channel) => _map[channel];
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
