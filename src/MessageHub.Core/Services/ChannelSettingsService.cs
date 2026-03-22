using MessageHub.Core;
using MessageHub.Core.Models;

namespace MessageHub.Core.Services;

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

    /// <summary>
    /// ICommonParameterProvider 實作 — 依鍵值取得配置參數。
    /// 支援的 key: "ChannelConfig" 回傳 ChannelConfig。
    /// </summary>
    public async Task<T?> GetParameterByKeyAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
    {
        if (key == "ChannelConfig" && typeof(T) == typeof(ChannelConfig))
        {
            var config = await GetAsync(cancellationToken);
            return config as T;
        }

        return null;
    }

    public async Task<ChannelConfig> GetAsync(CancellationToken cancellationToken = default)
    {
        var config = await store.LoadAsync(cancellationToken);
        return NormalizeConfig(config);
    }

    public async Task<ChannelConfig> SaveAsync(ChannelConfig config, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeConfig(config);
        return await store.SaveAsync(normalized, cancellationToken);
    }

    public IReadOnlyList<ChannelTypeDefinition> GetChannelTypes() => Definitions;

    public string GetSettingsFilePath() => store.GetFilePath();

    private static ChannelConfig NormalizeConfig(ChannelConfig config)
    {
        config.Channels ??= new Dictionary<string, ChannelSettings>(StringComparer.OrdinalIgnoreCase);
        config.Channels = config.Channels
            .Where(x => !string.IsNullOrWhiteSpace(x.Key))
            .ToDictionary(
                x => x.Key.Trim(),
                x => NormalizeSettings(x.Key.Trim(), x.Value),
                StringComparer.OrdinalIgnoreCase);

        return config;
    }

    private static ChannelSettings NormalizeSettings(string channelName, ChannelSettings settings)
    {
        var parameters = settings.Parameters
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && !string.IsNullOrWhiteSpace(pair.Value))
            .ToDictionary(pair => pair.Key.Trim(), pair => pair.Value.Trim(), StringComparer.OrdinalIgnoreCase);

        if (channelName.Equals("Line", StringComparison.OrdinalIgnoreCase))
        {
            Rename(parameters, "Token", "ChannelAccessToken");
            Rename(parameters, "Secret", "ChannelSecret");
        }
        else if (channelName.Equals("Telegram", StringComparison.OrdinalIgnoreCase))
        {
            Rename(parameters, "Token", "BotToken");
        }

        return new ChannelSettings
        {
            Enabled = settings.Enabled,
            Parameters = parameters
        };
    }

    private static void Rename(IDictionary<string, string> parameters, string oldKey, string newKey)
    {
        if (!parameters.ContainsKey(newKey) && parameters.TryGetValue(oldKey, out var value))
        {
            parameters[newKey] = value;
        }

        parameters.Remove(oldKey);
    }

}
