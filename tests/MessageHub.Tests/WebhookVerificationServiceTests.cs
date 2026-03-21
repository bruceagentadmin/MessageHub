using MessageHub.Application;
using MessageHub.Domain;
using MessageHub.Infrastructure;

namespace MessageHub.Tests;

public class WebhookVerificationServiceTests
{
    [Fact]
    public async Task VerifyAsync_ShouldReturnNotFound_WhenChannelDoesNotExist()
    {
        var service = new WebhookVerificationService(new FakeChannelSettingsService(new ChannelSettingsDocument()));

        var result = await service.VerifyAsync("missing");

        Assert.False(result.Success);
        Assert.Equal("Unknown", result.ChannelType);
        Assert.Contains("找不到指定頻道設定", result.Message);
    }

    [Fact]
    public async Task VerifyAsync_ShouldReturnMissingWebhookUrl_WhenWebhookUrlIsEmpty()
    {
        var service = new WebhookVerificationService(new FakeChannelSettingsService(new ChannelSettingsDocument
        {
            Channels = [new ChannelSettingsItem { Id = "Line_Main", Type = "Line", Enabled = true, Config = new Dictionary<string, string>() }]
        }));

        var result = await service.VerifyAsync("Line_Main");

        Assert.False(result.Success);
        Assert.Contains("WebhookUrl 未設定", result.Message);
    }

    [Fact]
    public async Task VerifyAsync_ShouldReturnMissingBotToken_WhenTelegramTokenIsEmpty()
    {
        var service = new WebhookVerificationService(new FakeChannelSettingsService(new ChannelSettingsDocument
        {
            Channels = [new ChannelSettingsItem
            {
                Id = "Telegram_Main",
                Type = "Telegram",
                Enabled = true,
                Config = new Dictionary<string, string> { ["WebhookUrl"] = "https://tg.test/webhook" }
            }]
        }));

        var result = await service.VerifyAsync("Telegram_Main");

        Assert.False(result.Success);
        Assert.Equal("bind", result.Action);
        Assert.Contains("BotToken 未設定", result.Message);
    }
}

file sealed class FakeChannelSettingsService(ChannelSettingsDocument document) : IChannelSettingsService
{
    public Task<ChannelSettingsDocument> GetAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(document);

    public Task<ChannelSettingsDocument> SaveAsync(ChannelSettingsDocument document, CancellationToken cancellationToken = default)
        => Task.FromResult(document);

    public IReadOnlyList<ChannelTypeDefinition> GetChannelTypes() => [];

    public string GetSettingsFilePath() => "/tmp/test.json";
}
