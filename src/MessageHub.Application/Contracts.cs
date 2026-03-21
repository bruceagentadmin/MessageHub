using MessageHub.Domain;

namespace MessageHub.Application;

public interface IMessageLogStore
{
    Task AddAsync(MessageLogEntry entry, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MessageLogEntry>> GetRecentAsync(int count, CancellationToken cancellationToken = default);
}

public interface IChannelClient
{
    string Name { get; }
    Task<InboundMessage> ParseAsync(string tenantId, WebhookTextMessageRequest request, CancellationToken cancellationToken = default);
    Task<MessageLogEntry> SendAsync(OutboundMessage message, CancellationToken cancellationToken = default);
}

public interface IChannelRegistry
{
    IReadOnlyList<ChannelDefinition> GetDefinitions();
    IChannelClient Get(string channel);
}

public interface IMessageOrchestrator
{
    Task<MessageLogEntry> HandleInboundAsync(string tenantId, string channel, WebhookTextMessageRequest request, CancellationToken cancellationToken = default);
    Task<MessageLogEntry> SendManualAsync(SendMessageRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MessageLogEntry>> GetRecentLogsAsync(int count, CancellationToken cancellationToken = default);
    IReadOnlyList<ChannelDefinition> GetChannels();
}
