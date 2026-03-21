using MessageHub.Application;
using MessageHub.Domain;

namespace MessageHub.Tests;

public class ChannelSettingsServiceTests
{
    [Fact]
    public async Task GetAsync_ShouldNormalizeLegacyLineAndTelegramKeys()
    {
        var store = new FakeChannelSettingsStore(new ChannelSettingsDocument
        {
            Channels =
            [
                new ChannelSettingsItem
                {
                    Id = " Line_Main ",
                    Type = "Line",
                    Enabled = true,
                    Config = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["Token"] = " line-token ",
                        ["Secret"] = " line-secret ",
                        ["WebhookUrl"] = " https://line.test/webhook "
                    }
                },
                new ChannelSettingsItem
                {
                    Id = " Telegram_Main ",
                    Type = "Telegram",
                    Enabled = true,
                    Config = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["Token"] = " telegram-token "
                    }
                }
            ]
        });

        var service = new ChannelSettingsService(store);
        var result = await service.GetAsync();

        var line = Assert.Single(result.Channels, x => x.Type == "Line");
        Assert.Equal("Line_Main", line.Id);
        Assert.Equal("line-token", line.Config["ChannelAccessToken"]);
        Assert.Equal("line-secret", line.Config["ChannelSecret"]);
        Assert.Equal("https://line.test/webhook", line.Config["WebhookUrl"]);
        Assert.False(line.Config.ContainsKey("Token"));
        Assert.False(line.Config.ContainsKey("Secret"));

        var telegram = Assert.Single(result.Channels, x => x.Type == "Telegram");
        Assert.Equal("Telegram_Main", telegram.Id);
        Assert.Equal("telegram-token", telegram.Config["BotToken"]);
        Assert.False(telegram.Config.ContainsKey("Token"));
    }

    [Fact]
    public async Task SaveAsync_ShouldDropInvalidChannelsAndEmptyConfigValues()
    {
        var store = new FakeChannelSettingsStore(new ChannelSettingsDocument());
        var service = new ChannelSettingsService(store);

        var saved = await service.SaveAsync(new ChannelSettingsDocument
        {
            Channels =
            [
                new ChannelSettingsItem
                {
                    Id = "  Line_Main  ",
                    Type = "Line",
                    Enabled = true,
                    Config = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["ChannelAccessToken"] = "  abc  ",
                        ["ChannelSecret"] = "   ",
                        ["WebhookMode"] = " ngrok "
                    }
                },
                new ChannelSettingsItem
                {
                    Id = "   ",
                    Type = "Telegram",
                    Enabled = true,
                    Config = new Dictionary<string, string>()
                }
            ]
        });

        var channel = Assert.Single(saved.Channels);
        Assert.Equal("Line_Main", channel.Id);
        Assert.Equal("abc", channel.Config["ChannelAccessToken"]);
        Assert.Equal("ngrok", channel.Config["WebhookMode"]);
        Assert.False(channel.Config.ContainsKey("ChannelSecret"));
    }
}

file sealed class FakeChannelSettingsStore(ChannelSettingsDocument document) : IChannelSettingsStore
{
    public ChannelSettingsDocument Document { get; private set; } = document;

    public Task<ChannelSettingsDocument> LoadAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(Document);

    public Task<ChannelSettingsDocument> SaveAsync(ChannelSettingsDocument document, CancellationToken cancellationToken = default)
    {
        Document = document;
        return Task.FromResult(Document);
    }

    public string GetFilePath() => "/tmp/test-channel-settings.json";
}
