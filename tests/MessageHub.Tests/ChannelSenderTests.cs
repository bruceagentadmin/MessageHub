using System.Net;
using System.Text;
using MessageHub.Core;
using MessageHub.Core.Channels;
using MessageHub.Core.Models;
using MessageHub.Core.Services;

namespace MessageHub.Tests;

public class ChannelSenderTests
{
    [Fact]
    public async Task TelegramChannel_ShouldThrow_WhenBotTokenMissing()
    {
        var service = new ChannelSettingsService(new FakeChannelSettingsStoreForSender(new ChannelConfig
        {
            Channels = new Dictionary<string, ChannelSettings>(StringComparer.OrdinalIgnoreCase)
            {
                ["telegram"] = new ChannelSettings { Enabled = true, Parameters = new Dictionary<string, string>() }
            }
        }));
        var channel = new TelegramChannel(service, new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK))));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => channel.SendAsync("chat-1", new OutboundMessage("tenant", "telegram", "chat-1", "hello")));

        Assert.Contains("BotToken 未設定", exception.Message);
    }

    [Fact]
    public async Task LineChannel_ShouldSendPushMessage_WhenTokenExists()
    {
        HttpRequestMessage? captured = null;
        var service = new ChannelSettingsService(new FakeChannelSettingsStoreForSender(new ChannelConfig
        {
            Channels = new Dictionary<string, ChannelSettings>(StringComparer.OrdinalIgnoreCase)
            {
                ["line"] = new ChannelSettings
                {
                    Enabled = true,
                    Parameters = new Dictionary<string, string> { ["ChannelAccessToken"] = "line-token" }
                }
            }
        }));
        var channel = new LineChannel(service, new HttpClient(new StubHttpMessageHandler(req =>
        {
            captured = req;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"ok\":true}", Encoding.UTF8, "application/json")
            };
        })));

        await channel.SendAsync("user-1", new OutboundMessage("tenant", "line", "user-1", "hello"));

        Assert.NotNull(captured);
        Assert.Equal("https://api.line.me/v2/bot/message/push", captured!.RequestUri!.ToString());
        Assert.Equal("Bearer", captured.Headers.Authorization?.Scheme);
        Assert.Equal("line-token", captured.Headers.Authorization?.Parameter);
    }
}

file sealed class FakeChannelSettingsStoreForSender(ChannelConfig config) : IChannelSettingsStore
{
    public Task<ChannelConfig> LoadAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(config);

    public Task<ChannelConfig> SaveAsync(ChannelConfig config, CancellationToken cancellationToken = default)
        => Task.FromResult(config);

    public string GetFilePath() => "/tmp/test.json";
}

file sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => Task.FromResult(handler(request));
}
