namespace MessageHub.Domain;

public sealed class ChannelSettingsDocument
{
    public List<ChannelSettingsItem> Channels { get; set; } = new();
}

public sealed class ChannelSettingsItem
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public Dictionary<string, string> Config { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed record ChannelConfigFieldDefinition(
    string Key,
    string Label,
    string Placeholder,
    bool Required,
    bool Secret = false);

public sealed record ChannelTypeDefinition(
    string Type,
    string DisplayName,
    IReadOnlyList<ChannelConfigFieldDefinition> Fields);
