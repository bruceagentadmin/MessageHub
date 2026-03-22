namespace MessageHub.Core.Models;

public sealed record ChannelTypeDefinition(
    string Type,
    string DisplayName,
    IReadOnlyList<ChannelConfigFieldDefinition> Fields);
