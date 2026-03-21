using System.Net;
using System.Text;
using MessageHub.Application;
using MessageHub.Domain;
using MessageHub.Infrastructure;

namespace MessageHub.Tests;

public class ChannelSenderTests
{
    [Fact]
    public async Task TelegramChannel_ShouldReturnFailed_WhenBotTokenMissing()
    {
        var service = new FakeChannelSettingsServiceForSender(new ChannelSettingsDocument
        {
            Channels = [new ChannelSettingsItem { Id = "tg", Type = "Telegram", Enabled = true, Config = new Dictionary<string, string>() }]
        });
        var channel = new TelegramChannel(service, new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK))));

        var result = await channel.SendAsync(new OutboundMessage("tenant", "telegram", "chat-1", "hello", DateTimeOffset.UtcNow));

        Assert.Equal(DeliveryStatus.Failed, result.Status);
        Assert.Contains("BotToken 未設定", result.Details);
    }

    [Fact]
    public async Task LineChannel_ShouldSendPushMessage_WhenTokenExists()
    {
        HttpRequestMessage? captured = null;
        var service = new FakeChannelSettingsServiceForSender(new ChannelSettingsDocument
        {
            Channels = [new ChannelSettingsItem
            {
                Id = "line",
                Type = "Line",
                Enabled = true,
                Config = new Dictionary<string, string> { ["ChannelAccessToken"] = "line-token" }
            }]
        });
        var channel = new LineChannel(service, new HttpClient(new StubHttpMessageHandler(req =>
        {
            captured = req;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"ok\":true}", Encoding.UTF8, "application/json")
            };
        })));

        var result = await channel.SendAsync(new OutboundMessage("tenant", "line", "user-1", "hello", DateTimeOffset.UtcNow));

        Assert.Equal(DeliveryStatus.Delivered, result.Status);
        Assert.NotNull(captured);
        Assert.Equal("https://api.line.me/v2/bot/message/push", captured!.RequestUri!.ToString());
        Assert.Equal("Bearer", captured.Headers.Authorization?.Scheme);
        Assert.Equal("line-token", captured.Headers.Authorization?.Parameter);
    }
}

file sealed class FakeChannelSettingsServiceForSender(ChannelSettingsDocument document) : IChannelSettingsService
{
    public Task<ChannelSettingsDocument> GetAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(document);

    public Task<ChannelSettingsDocument> SaveAsync(ChannelSettingsDocument document, CancellationToken cancellationToken = default)
        => Task.FromResult(document);

    public IReadOnlyList<ChannelTypeDefinition> GetChannelTypes() => [];

    public string GetSettingsFilePath() => "/tmp/test.json";
}

file sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => Task.FromResult(handler(request));
}
