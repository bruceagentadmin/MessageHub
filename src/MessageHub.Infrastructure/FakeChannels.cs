using System.Net.Http.Headers;
using System.Net.Http.Json;
using MessageHub.Core;

namespace MessageHub.Infrastructure;

public abstract class ConfiguredChannelBase(
    string name,
    string description,
    IChannelSettingsService channelSettingsService,
    HttpClient? httpClient = null) : IChannel
{
    public string Name => name;
    protected IChannelSettingsService ChannelSettingsService { get; } = channelSettingsService;
    protected HttpClient HttpClient { get; } = httpClient ?? new HttpClient();

    public Task<InboundMessage> ParseRequestAsync(string tenantId, WebhookTextMessageRequest request, CancellationToken cancellationToken = default)
    {
        var inbound = new InboundMessage(
            tenantId,
            Name,
            request.ChatId,
            request.SenderId,
            request.Content,
            DateTimeOffset.UtcNow,
            OriginalPayload: request.Content);

        return Task.FromResult(inbound);
    }

    public abstract Task<MessageLogEntry> SendAsync(string chatId, OutboundMessage message, ChannelSettings? settings = null, CancellationToken cancellationToken = default);

    public ChannelDefinition ToDefinition() => new(Name, true, true, description);

    protected async Task<ChannelSettings?> GetSettingsAsync(string channelType, CancellationToken cancellationToken)
    {
        var config = await ChannelSettingsService.GetAsync(cancellationToken);
        return config.Channels.FirstOrDefault(x => x.Enabled && x.Type.Equals(channelType, StringComparison.OrdinalIgnoreCase));
    }

    protected static MessageLogEntry BuildLog(OutboundMessage message, string channel, DeliveryStatus status, string source, string? details = null)
        => new(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            message.TenantId,
            channel,
            MessageDirection.Outbound,
            status,
            message.TargetId,
            message.Content,
            source,
            details);
}

public sealed class TelegramChannel(IChannelSettingsService channelSettingsService, HttpClient? httpClient = null) : ConfiguredChannelBase("telegram", "Telegram 真實通道，可接 webhook 與手動發送", channelSettingsService, httpClient)
{
    public override async Task<MessageLogEntry> SendAsync(string chatId, OutboundMessage message, ChannelSettings? settings = null, CancellationToken cancellationToken = default)
    {
        settings ??= await GetSettingsAsync("Telegram", cancellationToken);
        var botToken = settings?.Parameters.GetValueOrDefault("BotToken")?.Trim();
        if (string.IsNullOrWhiteSpace(botToken))
        {
            return BuildLog(message, Name, DeliveryStatus.Failed, "telegram sender", "BotToken 未設定");
        }

        var payload = new { chat_id = chatId, text = message.Content };
        using var response = await HttpClient.PostAsJsonAsync($"https://api.telegram.org/bot{botToken}/sendMessage", payload, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return BuildLog(message, Name, response.IsSuccessStatusCode ? DeliveryStatus.Delivered : DeliveryStatus.Failed, "telegram sender", body);
    }
}

public sealed class LineChannel(IChannelSettingsService channelSettingsService, HttpClient? httpClient = null) : ConfiguredChannelBase("line", "Line 真實通道，可接 webhook 與手動發送", channelSettingsService, httpClient)
{
    public override async Task<MessageLogEntry> SendAsync(string chatId, OutboundMessage message, ChannelSettings? settings = null, CancellationToken cancellationToken = default)
    {
        settings ??= await GetSettingsAsync("Line", cancellationToken);
        var token = settings?.Parameters.GetValueOrDefault("ChannelAccessToken")?.Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            return BuildLog(message, Name, DeliveryStatus.Failed, "line sender", "ChannelAccessToken 未設定");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.line.me/v2/bot/message/push");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(new
        {
            to = chatId,
            messages = new[] { new { type = "text", text = message.Content } }
        });

        using var response = await HttpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return BuildLog(message, Name, response.IsSuccessStatusCode ? DeliveryStatus.Delivered : DeliveryStatus.Failed, "line sender", body);
    }
}

public sealed class EmailChannel(IChannelSettingsService channelSettingsService) : ConfiguredChannelBase("email", "Email 模擬通道，先以文字送達紀錄驗證流程", channelSettingsService)
{
    public override Task<MessageLogEntry> SendAsync(string chatId, OutboundMessage message, ChannelSettings? settings = null, CancellationToken cancellationToken = default)
        => Task.FromResult(BuildLog(message, Name, DeliveryStatus.Delivered, "email mock sender", $"TriggeredBy={message.TriggeredBy ?? "Unknown"}"));
}
