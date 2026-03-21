using System.Net.Http.Headers;
using System.Net.Http.Json;
using MessageHub.Application;
using MessageHub.Domain;

namespace MessageHub.Infrastructure;

public abstract class ConfiguredChannelBase(
    string name,
    string description,
    IChannelSettingsService channelSettingsService) : IChannelClient
{
    public string Name => name;
    protected IChannelSettingsService ChannelSettingsService { get; } = channelSettingsService;
    protected HttpClient HttpClient { get; } = new();

    public Task<InboundMessage> ParseAsync(string tenantId, WebhookTextMessageRequest request, CancellationToken cancellationToken = default)
    {
        var inbound = new InboundMessage(
            tenantId,
            Name,
            request.ChatId,
            request.SenderId,
            request.Content,
            DateTimeOffset.UtcNow,
            RawPayload: request.Content);

        return Task.FromResult(inbound);
    }

    public abstract Task<MessageLogEntry> SendAsync(OutboundMessage message, CancellationToken cancellationToken = default);

    public ChannelDefinition ToDefinition() => new(Name, true, true, description);

    protected async Task<ChannelSettingsItem?> GetSettingsAsync(string channelType, CancellationToken cancellationToken)
    {
        var settings = await ChannelSettingsService.GetAsync(cancellationToken);
        return settings.Channels.FirstOrDefault(x => x.Enabled && x.Type.Equals(channelType, StringComparison.OrdinalIgnoreCase));
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

public sealed class TelegramChannel(IChannelSettingsService channelSettingsService) : ConfiguredChannelBase("telegram", "Telegram 真實通道，可接 webhook 與手動發送", channelSettingsService)
{
    public override async Task<MessageLogEntry> SendAsync(OutboundMessage message, CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync("Telegram", cancellationToken);
        var botToken = settings?.Config.GetValueOrDefault("BotToken")?.Trim();
        if (string.IsNullOrWhiteSpace(botToken))
        {
            return BuildLog(message, Name, DeliveryStatus.Failed, "telegram sender", "BotToken 未設定");
        }

        var payload = new { chat_id = message.TargetId, text = message.Content };
        using var response = await HttpClient.PostAsJsonAsync($"https://api.telegram.org/bot{botToken}/sendMessage", payload, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return BuildLog(message, Name, response.IsSuccessStatusCode ? DeliveryStatus.Delivered : DeliveryStatus.Failed, "telegram sender", body);
    }
}

public sealed class LineChannel(IChannelSettingsService channelSettingsService) : ConfiguredChannelBase("line", "Line 真實通道，可接 webhook 與手動發送", channelSettingsService)
{
    public override async Task<MessageLogEntry> SendAsync(OutboundMessage message, CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync("Line", cancellationToken);
        var token = settings?.Config.GetValueOrDefault("ChannelAccessToken")?.Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            return BuildLog(message, Name, DeliveryStatus.Failed, "line sender", "ChannelAccessToken 未設定");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.line.me/v2/bot/message/push");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(new
        {
            to = message.TargetId,
            messages = new[] { new { type = "text", text = message.Content } }
        });

        using var response = await HttpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return BuildLog(message, Name, response.IsSuccessStatusCode ? DeliveryStatus.Delivered : DeliveryStatus.Failed, "line sender", body);
    }
}

public sealed class EmailChannel(IChannelSettingsService channelSettingsService) : ConfiguredChannelBase("email", "Email 模擬通道，先以文字送達紀錄驗證流程", channelSettingsService)
{
    public override Task<MessageLogEntry> SendAsync(OutboundMessage message, CancellationToken cancellationToken = default)
        => Task.FromResult(BuildLog(message, Name, DeliveryStatus.Delivered, "email mock sender", $"TriggeredBy={message.TriggeredBy ?? "Unknown"}"));
}

public sealed class ChannelRegistry(IEnumerable<IChannelClient> channels) : IChannelRegistry
{
    private readonly IReadOnlyDictionary<string, IChannelClient> _lookup = channels.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
    private readonly IReadOnlyList<ChannelDefinition> _definitions = channels
        .Select(channel => channel is ConfiguredChannelBase configured ? configured.ToDefinition() : new ChannelDefinition(channel.Name, true, true, channel.Name))
        .ToArray();

    public IReadOnlyList<ChannelDefinition> GetDefinitions() => _definitions;

    public IChannelClient Get(string channel)
        => _lookup.TryGetValue(channel, out var client)
            ? client
            : throw new KeyNotFoundException($"找不到頻道：{channel}");
}
