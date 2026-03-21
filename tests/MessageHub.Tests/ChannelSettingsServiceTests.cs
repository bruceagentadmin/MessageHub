using MessageHub.Application;
using MessageHub.Core;

namespace MessageHub.Tests;

public class ChannelSettingsServiceTests
{
    [Fact]
    public async Task GetAsync_ShouldNormalizeLegacyLineAndTelegramKeys()
    {
        var store = new FakeChannelSettingsStore(new ChannelConfig
        {
            Channels =
            [
                new ChannelSettings
                {
                    Id = " Line_Main ",
                    Type = "Line",
                    Enabled = true,
                    Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["Token"] = " line-token ",
                        ["Secret"] = " line-secret ",
                        ["WebhookUrl"] = " https://line.test/webhook "
                    }
                },
                new ChannelSettings
                {
                    Id = " Telegram_Main ",
                    Type = "Telegram",
                    Enabled = true,
                    Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
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
        Assert.Equal("line-token", line.Parameters["ChannelAccessToken"]);
        Assert.Equal("line-secret", line.Parameters["ChannelSecret"]);
        Assert.Equal("https://line.test/webhook", line.Parameters["WebhookUrl"]);
        Assert.False(line.Parameters.ContainsKey("Token"));
        Assert.False(line.Parameters.ContainsKey("Secret"));

        var telegram = Assert.Single(result.Channels, x => x.Type == "Telegram");
        Assert.Equal("Telegram_Main", telegram.Id);
        Assert.Equal("telegram-token", telegram.Parameters["BotToken"]);
        Assert.False(telegram.Parameters.ContainsKey("Token"));
    }

    [Fact]
    public async Task SaveAsync_ShouldDropInvalidChannelsAndEmptyConfigValues()
    {
        var store = new FakeChannelSettingsStore(new ChannelConfig());
        var service = new ChannelSettingsService(store);

        var saved = await service.SaveAsync(new ChannelConfig
        {
            Channels =
            [
                new ChannelSettings
                {
                    Id = "  Line_Main  ",
                    Type = "Line",
                    Enabled = true,
                    Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["ChannelAccessToken"] = "  abc  ",
                        ["ChannelSecret"] = "   ",
                        ["WebhookMode"] = " ngrok "
                    }
                },
                new ChannelSettings
                {
                    Id = "   ",
                    Type = "Telegram",
                    Enabled = true,
                    Parameters = new Dictionary<string, string>()
                }
            ]
        });

        var channel = Assert.Single(saved.Channels);
        Assert.Equal("Line_Main", channel.Id);
        Assert.Equal("abc", channel.Parameters["ChannelAccessToken"]);
        Assert.Equal("ngrok", channel.Parameters["WebhookMode"]);
        Assert.False(channel.Parameters.ContainsKey("ChannelSecret"));
    }
}

file sealed class FakeChannelSettingsStore(ChannelConfig config) : IChannelSettingsStore
{
    public ChannelConfig Config { get; private set; } = config;

    public Task<ChannelConfig> LoadAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(Config);

    public Task<ChannelConfig> SaveAsync(ChannelConfig config, CancellationToken cancellationToken = default)
    {
        Config = config;
        return Task.FromResult(Config);
    }

    public string GetFilePath() => "/tmp/test-channel-settings.json";
}
