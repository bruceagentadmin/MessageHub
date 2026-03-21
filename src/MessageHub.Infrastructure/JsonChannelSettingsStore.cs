using System.Text.Json;
using MessageHub.Application;
using MessageHub.Domain;

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

    public async Task<ChannelSettingsDocument> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            var defaultDocument = CreateDefault();
            await SaveAsync(defaultDocument, cancellationToken);
            return defaultDocument;
        }

        await using var stream = File.OpenRead(_filePath);
        var document = await JsonSerializer.DeserializeAsync<ChannelSettingsDocument>(stream, JsonOptions, cancellationToken);
        return document ?? new ChannelSettingsDocument();
    }

    public async Task<ChannelSettingsDocument> SaveAsync(ChannelSettingsDocument document, CancellationToken cancellationToken = default)
    {
        await using var stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, document, JsonOptions, cancellationToken);
        return document;
    }

    public string GetFilePath() => _filePath;

    private static ChannelSettingsDocument CreateDefault() => new()
    {
        Channels =
        [
            new ChannelSettingsItem
            {
                Id = "Line_Main",
                Type = "Line",
                Enabled = true,
                Config = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["ChannelAccessToken"] = "",
                    ["ChannelSecret"] = "",
                    ["WebhookUrl"] = "https://3vmcf3ql-5001.jpe1.devtunnels.ms/api/line/webhook",
                    ["WebhookMode"] = "devtunnel"
                }
            },
            new ChannelSettingsItem
            {
                Id = "Telegram_Service",
                Type = "Telegram",
                Enabled = true,
                Config = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["BotToken"] = "",
                    ["WebhookUrl"] = "https://3vmcf3ql-5001.jpe1.devtunnels.ms/api/telegram/webhook",
                    ["WebhookMode"] = "devtunnel"
                }
            }
        ]
    };
}
