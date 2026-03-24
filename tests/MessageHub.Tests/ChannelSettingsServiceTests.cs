using MessageHub.Core;
using MessageHub.Core.Models;
using MessageHub.Domain;
using MessageHub.Domain.Services;

namespace MessageHub.Tests;

public class ChannelSettingsServiceTests
{
    [Fact]
    public async Task GetAsync_ShouldNormalizeLegacyLineAndTelegramKeys()
    {
        var store = new FakeChannelSettingsStore(new ChannelConfig
        {
            Channels =
            new Dictionary<string, ChannelSettings>(StringComparer.OrdinalIgnoreCase)
            {
                [" line "] = new ChannelSettings
                {
                    Enabled = true,
                    Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["Token"] = " line-token ",
                        ["Secret"] = " line-secret ",
                        ["WebhookUrl"] = " https://line.test/webhook "
                    }
                },
                [" telegram "] = new ChannelSettings
                {
                    Enabled = true,
                    Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["Token"] = " telegram-token "
                    }
                }
            }
        });

        var service = new ChannelSettingsService(store);
        var result = await service.GetAsync();

        var line = Assert.Single(result.Channels, x => x.Key.Equals("line", StringComparison.OrdinalIgnoreCase));
        Assert.True(line.Value.Enabled);
        Assert.Equal("line-token", line.Value.Parameters["ChannelAccessToken"]);
        Assert.Equal("line-secret", line.Value.Parameters["ChannelSecret"]);
        Assert.Equal("https://line.test/webhook", line.Value.Parameters["WebhookUrl"]);
        Assert.False(line.Value.Parameters.ContainsKey("Token"));
        Assert.False(line.Value.Parameters.ContainsKey("Secret"));

        var telegram = Assert.Single(result.Channels, x => x.Key.Equals("telegram", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("telegram-token", telegram.Value.Parameters["BotToken"]);
        Assert.False(telegram.Value.Parameters.ContainsKey("Token"));
    }

    [Fact]
    public async Task SaveAsync_ShouldDropInvalidChannelsAndEmptyConfigValues()
    {
        var store = new FakeChannelSettingsStore(new ChannelConfig());
        var service = new ChannelSettingsService(store);

        var saved = await service.SaveAsync(new ChannelConfig
        {
            Channels =
            new Dictionary<string, ChannelSettings>(StringComparer.OrdinalIgnoreCase)
            {
                ["  line  "] = new ChannelSettings
                {
                    Enabled = true,
                    Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["ChannelAccessToken"] = "  abc  ",
                        ["ChannelSecret"] = "   ",
                        ["WebhookMode"] = " ngrok "
                    }
                },
                ["   "] = new ChannelSettings
                {
                    Enabled = true,
                    Parameters = new Dictionary<string, string>()
                }
            }
        });

        var channel = Assert.Single(saved.Channels);
        Assert.Equal("line", channel.Key);
        Assert.Equal("abc", channel.Value.Parameters["ChannelAccessToken"]);
        Assert.Equal("ngrok", channel.Value.Parameters["WebhookMode"]);
        Assert.False(channel.Value.Parameters.ContainsKey("ChannelSecret"));
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
