using MessageHub.Api.Controllers;
using MessageHub.Core;
using MessageHub.Core.Models;
using MessageHub.Core.Services;
using MessageHub.Domain.Models;
using MessageHub.Domain.Repositories;
using MessageHub.Domain.Services;
using Microsoft.AspNetCore.Mvc;

namespace MessageHub.Tests;

public class ControlCenterControllerTests
{
    private static ControlCenterController CreateController()
    {
        var logStore = new FakeLogStore();
        var messageBus = new ControllerFakeMessageBus();
        var factory = new ChannelFactory([new FakeChannel("telegram")]);
        var recentTargets = new FakeRecentTargetStore();
        var processor = new EchoMessageProcessor();
        var coordinator = new MessageCoordinator(logStore, factory, recentTargets, messageBus, processor);
        var settingsService = new ChannelSettingsService(new FakeSettingsStoreForController());
        var webhookVerification = new FakeWebhookVerificationServiceForController();
        var contactRepository = new FakeContactRepository();
        IMessagingService messagingService = new MessagingService(coordinator, settingsService, webhookVerification, contactRepository);
        return new ControlCenterController(messagingService);
    }

    [Fact]
    public async Task Send_ShouldReturnBadRequest_WhenRequiredFieldsMissing()
    {
        var controller = CreateController();

        var result = await controller.Send(new SendMessageRequest("", "", "", "", null), default);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Contains("tenantId, channel, content 必填", badRequest.Value?.ToString());
    }

    [Fact]
    public async Task Send_ShouldReturnOk_WhenPayloadIsValid()
    {
        var controller = CreateController();

        var result = await controller.Send(new SendMessageRequest("tenant", "telegram", "chat-1", "hello", "test"), default);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var log = Assert.IsType<MessageLogEntry>(ok.Value);
        Assert.Equal("telegram", log.Channel);
        Assert.Equal("chat-1", log.TargetId);
        Assert.Equal(DeliveryStatus.Pending, log.Status);
    }

    [Fact]
    public async Task VerifyWebhook_ShouldReturnBadRequest_WhenChannelIdMissing()
    {
        var controller = CreateController();

        var result = await controller.VerifyWebhook(new WebhookVerifyRequest(""), default);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }
}

file sealed class ControllerFakeMessageBus : IMessageBus
{
    public ValueTask PublishOutboundAsync(OutboundMessage message, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    public async IAsyncEnumerable<OutboundMessage> ConsumeOutboundAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        yield break;
    }

    public ValueTask PublishInboundAsync(InboundMessage message, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    public async IAsyncEnumerable<InboundMessage> ConsumeInboundAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        yield break;
    }

    public ValueTask PublishDeadLetterAsync(DeadLetterMessage message, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    public async IAsyncEnumerable<DeadLetterMessage> ConsumeDeadLetterAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        yield break;
    }
}

file sealed class FakeLogStore : IMessageLogStore
{
    public Task AddAsync(MessageLogEntry entry, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<IReadOnlyList<MessageLogEntry>> GetRecentAsync(int count, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<MessageLogEntry>>([]);
}

file sealed class FakeChannel(string name) : IChannel
{
    public string Name => name;

    public Task<InboundMessage> ParseRequestAsync(string tenantId, WebhookTextMessageRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(new InboundMessage(tenantId, Name, request.ChatId, request.SenderId, request.Content, DateTimeOffset.UtcNow));

    public Task SendAsync(string chatId, OutboundMessage message, ChannelSettings? settings = null, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

file sealed class FakeRecentTargetStore : IRecentTargetStore
{
    public Task SetLastTargetAsync(string channel, string targetId, string? displayName = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<RecentTargetInfo?> GetLastTargetAsync(string channel, CancellationToken cancellationToken = default)
        => Task.FromResult<RecentTargetInfo?>(null);
}

file sealed class FakeSettingsStoreForController : IChannelSettingsStore
{
    public Task<ChannelConfig> LoadAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new ChannelConfig());
    public Task<ChannelConfig> SaveAsync(ChannelConfig config, CancellationToken cancellationToken = default)
        => Task.FromResult(config);
    public string GetFilePath() => "/tmp/test.json";
}

file sealed class FakeWebhookVerificationServiceForController : IWebhookVerificationService
{
    public Task<WebhookVerifyResult> VerifyAsync(string channelId, CancellationToken cancellationToken = default)
        => Task.FromResult(new WebhookVerifyResult(channelId, "Line", true, "verify", 200, "ok"));
}

file sealed class FakeContactRepository : IContactRepository
{
    public Task UpsertAsync(Contact contact, CancellationToken ct = default) => Task.CompletedTask;
    public Task<IReadOnlyList<Contact>> GetByChannelAsync(string channel, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Contact>>([]);
    public Task<IReadOnlyList<Contact>> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Contact>>([]);
    public Task<Contact?> FindAsync(string channel, string platformUserId, CancellationToken ct = default)
        => Task.FromResult<Contact?>(null);
}
