using System.Text.Json;
using MessageHub.Core;

namespace MessageHub.Infrastructure;

public sealed class JsonChannelSettingsStore : IChannelSettingsStore
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

    public async Task<ChannelConfig> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            var defaultConfig = CreateDefault();
            await SaveAsync(defaultConfig, cancellationToken);
            return defaultConfig;
        }

        await using var stream = File.OpenRead(_filePath);
        var config = await JsonSerializer.DeserializeAsync<ChannelConfig>(stream, JsonOptions, cancellationToken);
        return config ?? new ChannelConfig();
    }

    public async Task<ChannelConfig> SaveAsync(ChannelConfig config, CancellationToken cancellationToken = default)
    {
        await using var stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, config, JsonOptions, cancellationToken);
        return config;
    }

    public string GetFilePath() => _filePath;

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
}
