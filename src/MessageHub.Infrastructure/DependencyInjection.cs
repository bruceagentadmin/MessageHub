using MessageHub.Core;
using Microsoft.Extensions.DependencyInjection;

namespace MessageHub.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddMessageHubInfrastructure(this IServiceCollection services)
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

        // MessageBus
        services.AddSingleton<MessageBus>();
        services.AddSingleton<IMessageBus>(sp => sp.GetRequiredService<MessageBus>());

        // ChannelManager (背景 Worker)
        services.AddHostedService<ChannelManager>();

        // Notification service
        services.AddSingleton<INotificationService, NotificationService>();

        // Webhook verification
        services.AddSingleton<IWebhookVerificationService, WebhookVerificationService>();

        return services;
    }
}
