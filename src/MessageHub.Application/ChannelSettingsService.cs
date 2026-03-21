using MessageHub.Domain;

namespace MessageHub.Application;

public sealed class ChannelSettingsService(IChannelSettingsStore store) : IChannelSettingsService
{
    private static readonly IReadOnlyList<ChannelTypeDefinition> Definitions =
    [
        new(
            "Line",
            "LINE",
            [
                new ChannelConfigFieldDefinition("ChannelAccessToken", "Channel Access Token", "輸入 LINE channel access token", true, true),
                new ChannelConfigFieldDefinition("ChannelSecret", "Channel Secret（POC 可留空）", "若目前是 dev tunnel / POC，可先留空", false, true),
                new ChannelConfigFieldDefinition("WebhookUrl", "Webhook URL", "例如 https://xxxxx.devtunnels.ms/api/line/webhook", false),
                new ChannelConfigFieldDefinition("WebhookMode", "Webhook 模式", "例如 devtunnel / production", false)
            ]),
        new(
            "Telegram",
            "Telegram",
            [
                new ChannelConfigFieldDefinition("BotToken", "Bot Token", "輸入 Telegram bot token", true, true),
                new ChannelConfigFieldDefinition("WebhookUrl", "Webhook URL", "例如 https://xxxxx.devtunnels.ms/api/telegram/webhook", false),
                new ChannelConfigFieldDefinition("WebhookMode", "Webhook 模式", "例如 devtunnel / production", false)
            ]),
        new(
            "Email",
            "Email",
            [
                new ChannelConfigFieldDefinition("Host", "SMTP Host", "例如 smtp.gmail.com", true),
                new ChannelConfigFieldDefinition("Port", "SMTP Port", "例如 587", true),
                new ChannelConfigFieldDefinition("Username", "帳號", "例如 service@example.com", true),
                new ChannelConfigFieldDefinition("Password", "密碼", "輸入 SMTP 密碼", true, true)
            ])
    ];

    public async Task<ChannelSettingsDocument> GetAsync(CancellationToken cancellationToken = default)
    {
        var document = await store.LoadAsync(cancellationToken);
        return NormalizeDocument(document);
    }

    public async Task<ChannelSettingsDocument> SaveAsync(ChannelSettingsDocument document, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeDocument(document);
        return await store.SaveAsync(normalized, cancellationToken);
    }

    public IReadOnlyList<ChannelTypeDefinition> GetChannelTypes() => Definitions;

    public string GetSettingsFilePath() => store.GetFilePath();

    private static ChannelSettingsDocument NormalizeDocument(ChannelSettingsDocument document)
    {
        document.Channels ??= [];
        document.Channels = document.Channels
            .Where(x => !string.IsNullOrWhiteSpace(x.Id) && !string.IsNullOrWhiteSpace(x.Type))
            .Select(NormalizeItem)
            .ToList();

        return document;
    }

    private static ChannelSettingsItem NormalizeItem(ChannelSettingsItem item)
    {
        var config = item.Config
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && !string.IsNullOrWhiteSpace(pair.Value))
            .ToDictionary(pair => pair.Key.Trim(), pair => pair.Value.Trim(), StringComparer.OrdinalIgnoreCase);

        if (item.Type.Equals("Line", StringComparison.OrdinalIgnoreCase))
        {
            Rename(config, "Token", "ChannelAccessToken");
            Rename(config, "Secret", "ChannelSecret");
        }
        else if (item.Type.Equals("Telegram", StringComparison.OrdinalIgnoreCase))
        {
            Rename(config, "Token", "BotToken");
        }

        return new ChannelSettingsItem
        {
            Id = item.Id.Trim(),
            Type = item.Type.Trim(),
            Enabled = item.Enabled,
            Config = config
        };
    }

    private static void Rename(IDictionary<string, string> config, string oldKey, string newKey)
    {
        if (!config.ContainsKey(newKey) && config.TryGetValue(oldKey, out var value))
        {
            config[newKey] = value;
        }

        config.Remove(oldKey);
    }
}
