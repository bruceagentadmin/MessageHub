namespace MessageHub.Core.Models;

public sealed record ChannelConfigFieldDefinition(
    string Key,
    string Label,
    string Placeholder,
    bool Required,
    bool Secret = false);
