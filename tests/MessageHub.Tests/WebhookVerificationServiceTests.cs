using MessageHub.Core;
using MessageHub.Core.Models;
using MessageHub.Domain;
using MessageHub.Domain.Services;

namespace MessageHub.Tests;

public class WebhookVerificationServiceTests
{
    [Fact]
    public async Task VerifyAsync_ShouldReturnNotFound_WhenChannelDoesNotExist()
    {
        var service = new WebhookVerificationService(new ChannelSettingsService(new FakeSettingsStore(new ChannelConfig())));

        var result = await service.VerifyAsync("missing");

        Assert.False(result.Success);
        Assert.Equal("Unknown", result.ChannelType);
        Assert.Contains("找不到指定頻道設定", result.Message);
    }

    [Fact]
    public async Task VerifyAsync_ShouldReturnMissingWebhookUrl_WhenWebhookUrlIsEmpty()
    {
        var service = new WebhookVerificationService(new ChannelSettingsService(new FakeSettingsStore(new ChannelConfig
        {
            Channels = new Dictionary<string, ChannelSettings>(StringComparer.OrdinalIgnoreCase)
            {
                ["line"] = new ChannelSettings { Enabled = true, Parameters = new Dictionary<string, string>() }
            }
        })));

        var result = await service.VerifyAsync("line");

        Assert.False(result.Success);
        Assert.Contains("WebhookUrl 未設定", result.Message);
    }

    [Fact]
    public async Task VerifyAsync_ShouldReturnMissingBotToken_WhenTelegramTokenIsEmpty()
    {
        var service = new WebhookVerificationService(new ChannelSettingsService(new FakeSettingsStore(new ChannelConfig
        {
            Channels = new Dictionary<string, ChannelSettings>(StringComparer.OrdinalIgnoreCase)
            {
                ["telegram"] = new ChannelSettings
                {
                    Enabled = true,
                    Parameters = new Dictionary<string, string> { ["WebhookUrl"] = "https://tg.test/webhook" }
                }
            }
        })));

        var result = await service.VerifyAsync("telegram");

        Assert.False(result.Success);
        Assert.Equal("bind", result.Action);
        Assert.Contains("BotToken 未設定", result.Message);
    }
}

file sealed class FakeSettingsStore(ChannelConfig config) : IChannelSettingsStore
{
    public Task<ChannelConfig> LoadAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(config);

    public Task<ChannelConfig> SaveAsync(ChannelConfig config, CancellationToken cancellationToken = default)
        => Task.FromResult(config);

    public string GetFilePath() => "/tmp/test.json";
}
