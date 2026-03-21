namespace MessageHub.Core;

public sealed record ChannelDefinition(
    string Name,
    bool SupportsInbound,
    bool SupportsOutbound,
    string Description);
