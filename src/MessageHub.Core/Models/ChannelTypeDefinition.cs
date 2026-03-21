namespace MessageHub.Core;

public sealed record ChannelTypeDefinition(
    string Type,
    string DisplayName,
    IReadOnlyList<ChannelConfigFieldDefinition> Fields);
