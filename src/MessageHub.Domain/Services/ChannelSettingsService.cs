using MessageHub.Core;
using MessageHub.Core.Models;

namespace MessageHub.Domain.Services;

/// <summary>
/// 頻道設定服務 — 提供頻道設定的讀取、儲存與正規化功能，
/// 同時實作 <see cref="IChannelSettingsService"/> 與 <see cref="ICommonParameterProvider"/> 介面。
/// <para>
/// 主要職責：
/// <list type="bullet">
///   <item>委派 <see cref="IChannelSettingsStore"/> 進行 JSON 持久化讀寫</item>
///   <item>每次讀取或儲存時，自動執行設定正規化（去除空白鍵值、重命名舊版參數鍵名）</item>
///   <item>提供各頻道類型的欄位定義清單（<see cref="ChannelTypeDefinition"/>）</item>
///   <item>透過 <see cref="ICommonParameterProvider"/> 讓其他服務以鍵值方式取得 <see cref="ChannelConfig"/></item>
/// </list>
/// </para>
/// </summary>
public sealed class ChannelSettingsService(IChannelSettingsStore store) : IChannelSettingsService, ICommonParameterProvider
{
    /// <summary>
    /// 各頻道類型的欄位定義清單，描述每個頻道需要哪些設定欄位、是否必填、是否為敏感資訊。
    /// 此清單供前端動態產生設定表單使用。
    /// </summary>
    private static readonly IReadOnlyList<ChannelTypeDefinition> Definitions =
    [
        // LINE 頻道：需要 ChannelAccessToken（必填敏感）與 ChannelSecret（POC 可留空）
        new(
            "Line",
            "LINE",
            [
                new ChannelConfigFieldDefinition("ChannelAccessToken", "Channel Access Token", "輸入 LINE channel access token", true, true),
                new ChannelConfigFieldDefinition("ChannelSecret", "Channel Secret（POC 可留空）", "若目前是 dev tunnel / POC，可先留空", false, true),
                new ChannelConfigFieldDefinition("WebhookUrl", "Webhook URL", "例如 https://xxxxx.devtunnels.ms/api/line/webhook", false),
                new ChannelConfigFieldDefinition("WebhookMode", "Webhook 模式", "例如 devtunnel / production", false)
            ]),
        // Telegram 頻道：需要 BotToken（必填敏感）
        new(
            "Telegram",
            "Telegram",
            [
                new ChannelConfigFieldDefinition("BotToken", "Bot Token", "輸入 Telegram bot token", true, true),
                new ChannelConfigFieldDefinition("WebhookUrl", "Webhook URL", "例如 https://xxxxx.devtunnels.ms/api/telegram/webhook", false),
                new ChannelConfigFieldDefinition("WebhookMode", "Webhook 模式", "例如 devtunnel / production", false)
            ]),
        // Email 頻道：需要 SMTP 連線資訊（Host、Port、Username、Password）
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

    /// <inheritdoc />
    public async Task<T?> GetParameterByKeyAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
    {
        if (key == "ChannelConfig" && typeof(T) == typeof(ChannelConfig))
        {
            var config = await GetAsync(cancellationToken);
            return config as T;
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<ChannelConfig> GetAsync(CancellationToken cancellationToken = default)
    {
        var config = await store.LoadAsync(cancellationToken);
        return NormalizeConfig(config);
    }

    /// <inheritdoc />
    public async Task<ChannelConfig> SaveAsync(ChannelConfig config, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeConfig(config);
        return await store.SaveAsync(normalized, cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyList<ChannelTypeDefinition> GetChannelTypes() => Definitions;

    /// <inheritdoc />
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
