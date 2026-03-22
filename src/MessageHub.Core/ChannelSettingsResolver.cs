namespace MessageHub.Core;

public static class ChannelSettingsResolver
{
    public static ChannelSettings? FindSettings(ChannelConfig config, string channelName)
    {
        if (config.Channels.TryGetValue(channelName, out var direct))
        {
            return direct;
        }

        var normalized = channelName.Trim();
        var match = config.Channels.FirstOrDefault(x =>
            x.Key.Equals(normalized, StringComparison.OrdinalIgnoreCase)
            || x.Key.StartsWith(normalized + "_", StringComparison.OrdinalIgnoreCase)
            || x.Key.EndsWith("_" + normalized, StringComparison.OrdinalIgnoreCase)
            || x.Key.Contains(normalized, StringComparison.OrdinalIgnoreCase)
            || LooksLikeChannelType(x.Key, x.Value, normalized));

        return string.IsNullOrWhiteSpace(match.Key) ? null : match.Value;
    }

    private static bool LooksLikeChannelType(string key, ChannelSettings settings, string channelName)
    {
        var haystack = string.Join(' ', new[]
        {
            key,
            string.Join(' ', settings.Parameters.Keys)
        }).ToLowerInvariant();

        return channelName.ToLowerInvariant() switch
        {
            "telegram" => haystack.Contains("telegram") || haystack.Contains("bottoken"),
            "line" => haystack.Contains("line") || haystack.Contains("channelaccesstoken") || haystack.Contains("channelsecret"),
            "email" => haystack.Contains("email") || haystack.Contains("smtp") || haystack.Contains("host"),
            _ => false
        };
    }
}
