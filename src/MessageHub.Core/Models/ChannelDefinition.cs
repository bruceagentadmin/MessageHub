namespace MessageHub.Core.Models;

public sealed record ChannelDefinition(
    string Name,
    bool SupportsInbound,
    bool SupportsOutbound,
    string Description);
