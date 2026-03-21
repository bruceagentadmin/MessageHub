using MessageHub.Api.Controllers;
using MessageHub.Application;
using MessageHub.Domain;
using Microsoft.AspNetCore.Mvc;

namespace MessageHub.Tests;

public class ControlCenterControllerTests
{
    [Fact]
    public async Task Send_ShouldReturnBadRequest_WhenRequiredFieldsMissing()
    {
        var controller = new ControlCenterController(new FakeOrchestrator(), new FakeSettingsService(), new FakeWebhookVerificationService());

        var result = await controller.Send(new SendMessageRequest("", "", "", "", null), default);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Contains("tenantId, channel, content 必填", badRequest.Value?.ToString());
    }

    [Fact]
    public async Task Send_ShouldReturnOk_WhenPayloadIsValid()
    {
        var controller = new ControlCenterController(new FakeOrchestrator(), new FakeSettingsService(), new FakeWebhookVerificationService());

        var result = await controller.Send(new SendMessageRequest("tenant", "telegram", "chat-1", "hello", "test"), default);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var log = Assert.IsType<MessageLogEntry>(ok.Value);
        Assert.Equal("telegram", log.Channel);
        Assert.Equal("chat-1", log.TargetId);
    }

    [Fact]
    public async Task VerifyWebhook_ShouldReturnBadRequest_WhenChannelIdMissing()
    {
        var controller = new ControlCenterController(new FakeOrchestrator(), new FakeSettingsService(), new FakeWebhookVerificationService());

        var result = await controller.VerifyWebhook(new WebhookVerifyRequest(""), default);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }
}

file sealed class FakeOrchestrator : IMessageOrchestrator
{
    public Task<MessageLogEntry> HandleInboundAsync(string tenantId, string channel, WebhookTextMessageRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(new MessageLogEntry(Guid.NewGuid(), DateTimeOffset.UtcNow, tenantId, channel, MessageDirection.Inbound, DeliveryStatus.Delivered, request.ChatId, request.Content, "fake"));

    public Task<IReadOnlyList<MessageLogEntry>> GetRecentLogsAsync(int count, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<MessageLogEntry>>([]);

    public IReadOnlyList<ChannelDefinition> GetChannels()
        => [new ChannelDefinition("telegram", true, true, "fake")];

    public Task<MessageLogEntry> SendManualAsync(SendMessageRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(new MessageLogEntry(Guid.NewGuid(), DateTimeOffset.UtcNow, request.TenantId, request.Channel, MessageDirection.Outbound, DeliveryStatus.Delivered, request.TargetId, request.Content, "fake"));
}

file sealed class FakeSettingsService : IChannelSettingsService
{
    public Task<ChannelSettingsDocument> GetAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new ChannelSettingsDocument());

    public Task<ChannelSettingsDocument> SaveAsync(ChannelSettingsDocument document, CancellationToken cancellationToken = default)
        => Task.FromResult(document);

    public IReadOnlyList<ChannelTypeDefinition> GetChannelTypes() => [];

    public string GetSettingsFilePath() => "/tmp/test.json";
}

file sealed class FakeWebhookVerificationService : IWebhookVerificationService
{
    public Task<WebhookVerifyResult> VerifyAsync(string channelId, CancellationToken cancellationToken = default)
        => Task.FromResult(new WebhookVerifyResult(channelId, "Line", true, "verify", 200, "ok"));
}
