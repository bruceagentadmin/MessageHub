using MessageHub.Application;
using MessageHub.Domain;

namespace MessageHub.Infrastructure;

public abstract class FakeChannelBase(string name, string description) : IChannelClient
{
    public string Name => name;

    public Task<InboundMessage> ParseAsync(string tenantId, WebhookTextMessageRequest request, CancellationToken cancellationToken = default)
    {
        var inbound = new InboundMessage(
            tenantId,
            Name,
            request.ChatId,
            request.SenderId,
            request.Content,
            DateTimeOffset.UtcNow,
            RawPayload: request.Content);

        return Task.FromResult(inbound);
    }

    public Task<MessageLogEntry> SendAsync(OutboundMessage message, CancellationToken cancellationToken = default)
    {
        var entry = new MessageLogEntry(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            message.TenantId,
            Name,
            MessageDirection.Outbound,
            DeliveryStatus.Delivered,
            message.TargetId,
            message.Content,
            $"{Name} mock sender",
            $"TriggeredBy={message.TriggeredBy ?? "Unknown"}");

        return Task.FromResult(entry);
    }

    public ChannelDefinition ToDefinition() => new(Name, true, true, description);
}

public sealed class TelegramChannel() : FakeChannelBase("telegram", "Telegram 模擬通道，可接 webhook 與手動發送")
{
}

public sealed class LineChannel() : FakeChannelBase("line", "Line 模擬通道，可接 webhook 與手動發送")
{
}

public sealed class EmailChannel() : FakeChannelBase("email", "Email 模擬通道，先以文字送達紀錄驗證流程")
{
}

public sealed class ChannelRegistry(IEnumerable<IChannelClient> channels) : IChannelRegistry
{
    private readonly IReadOnlyDictionary<string, IChannelClient> _lookup = channels.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
    private readonly IReadOnlyList<ChannelDefinition> _definitions = channels
        .Select(channel => channel is FakeChannelBase fake ? fake.ToDefinition() : new ChannelDefinition(channel.Name, true, true, channel.Name))
        .ToArray();

    public IReadOnlyList<ChannelDefinition> GetDefinitions() => _definitions;

    public IChannelClient Get(string channel)
        => _lookup.TryGetValue(channel, out var client)
            ? client
            : throw new KeyNotFoundException($"找不到頻道：{channel}");
}
