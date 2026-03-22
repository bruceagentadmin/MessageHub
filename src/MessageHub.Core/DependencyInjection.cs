using MessageHub.Core.Bus;
using MessageHub.Core.Channels;
using MessageHub.Core.Services;
using MessageHub.Core.Stores;
using Microsoft.Extensions.DependencyInjection;

namespace MessageHub.Core;

public static class DependencyInjection
{
    public static IServiceCollection AddMessageHubCore(this IServiceCollection services)
    {
        // Stores
        services.AddSingleton<IMessageLogStore, InMemoryMessageLogStore>();
        services.AddSingleton<IRecentTargetStore, RecentTargetStore>();
        services.AddSingleton<IChannelSettingsStore, JsonChannelSettingsStore>();

        // Channels
        services.AddSingleton<IChannel, TelegramChannel>();
        services.AddSingleton<IChannel, LineChannel>();
        services.AddSingleton<IChannel, EmailChannel>();
        services.AddSingleton<ChannelFactory>();

        // MessageBus (三通道: Outbound + Inbound + DLQ)
        services.AddSingleton<MessageBus>();
        services.AddSingleton<IMessageBus>(sp => sp.GetRequiredService<MessageBus>());

        // Services
        services.AddSingleton<ChannelSettingsService>();
        services.AddSingleton<IChannelSettingsService>(sp => sp.GetRequiredService<ChannelSettingsService>());
        services.AddSingleton<ICommonParameterProvider>(sp => sp.GetRequiredService<ChannelSettingsService>());
        services.AddSingleton<UnifiedMessageProcessor>();
        services.AddSingleton<IMessageProcessor>(sp => sp.GetRequiredService<UnifiedMessageProcessor>());

        // Notification service
        services.AddSingleton<INotificationService, NotificationService>();

        // Webhook verification
        services.AddSingleton<IWebhookVerificationService, WebhookVerificationService>();

        // ChannelManager (背景 Worker: 重試 + DLQ + 限流)
        services.AddHostedService<ChannelManager>();

        return services;
    }
}
