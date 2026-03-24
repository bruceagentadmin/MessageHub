using System.Text.Json;
using MessageHub.Core.Models;

namespace MessageHub.Domain.Stores;

/// <summary>
/// 以 JSON 檔案為後端的頻道設定持久化儲存實作。
/// 設定檔存放於應用程式根目錄上層五層的 <c>data/channel-settings.json</c>，
/// 確保在多環境（開發、容器、IIS）下路徑一致。
/// 支援新版格式（<see cref="ChannelConfig"/>）與舊版格式（<see cref="LegacyChannelConfig"/>）的反序列化回退。
/// </summary>
internal sealed class JsonChannelSettingsStore : IChannelSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = null
    };

    private readonly string _filePath;

    public JsonChannelSettingsStore()
    {
        var baseDirectory = AppContext.BaseDirectory;
        var dataDirectory = Path.GetFullPath(Path.Combine(baseDirectory, "..", "..", "..", "..", "..", "data"));
        Directory.CreateDirectory(dataDirectory);
        _filePath = Path.Combine(dataDirectory, "channel-settings.json");
    }

    /// <inheritdoc />
    public async Task<ChannelConfig> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            var defaultConfig = CreateDefault();
            await SaveAsync(defaultConfig, cancellationToken);
            return defaultConfig;
        }

        var json = await File.ReadAllTextAsync(_filePath, cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new ChannelConfig();
        }

        var config = TryDeserializeCurrent(json) ?? TryDeserializeLegacy(json);
        return config ?? new ChannelConfig();
    }

    /// <inheritdoc />
    public async Task<ChannelConfig> SaveAsync(ChannelConfig config, CancellationToken cancellationToken = default)
    {
        await using var stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, config, JsonOptions, cancellationToken);
        return config;
    }

    /// <inheritdoc />
    public string GetFilePath() => _filePath;

    private static ChannelConfig? TryDeserializeCurrent(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<ChannelConfig>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static ChannelConfig? TryDeserializeLegacy(string json)
    {
        try
        {
            var legacy = JsonSerializer.Deserialize<LegacyChannelConfig>(json, JsonOptions);
            if (legacy?.Channels is null)
            {
                return null;
            }

            var result = new ChannelConfig();
            foreach (var item in legacy.Channels)
            {
                if (string.IsNullOrWhiteSpace(item.Id))
                {
                    continue;
                }

                result.Channels[item.Id.Trim()] = new ChannelSettings
                {
                    Enabled = item.Enabled,
                    Parameters = new Dictionary<string, string>(item.Config ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase)
                };
            }

            return result;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static ChannelConfig CreateDefault() => new()
    {
        Channels =
        new Dictionary<string, ChannelSettings>(StringComparer.OrdinalIgnoreCase)
        {
            ["line"] = new ChannelSettings
            {
                Enabled = true,
                Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["ChannelAccessToken"] = "",
                    ["ChannelSecret"] = "",
                    ["WebhookUrl"] = "https://3vmcf3ql-5001.jpe1.devtunnels.ms/api/line/webhook",
                    ["WebhookMode"] = "devtunnel"
                }
            },
            ["telegram"] = new ChannelSettings
            {
                Enabled = true,
                Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["BotToken"] = "",
                    ["WebhookUrl"] = "https://3vmcf3ql-5001.jpe1.devtunnels.ms/api/telegram/webhook",
                    ["WebhookMode"] = "devtunnel"
                }
            }
        }
    };

    private sealed class LegacyChannelConfig
    {
        public List<LegacyChannelItem> Channels { get; set; } = [];
    }

    private sealed class LegacyChannelItem
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool Enabled { get; set; }
        public Dictionary<string, string> Config { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
