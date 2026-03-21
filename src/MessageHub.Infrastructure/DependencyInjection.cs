using MessageHub.Application;
using Microsoft.Extensions.DependencyInjection;

namespace MessageHub.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddMessageHubInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IMessageLogStore, InMemoryMessageLogStore>();
        services.AddSingleton<IRecentTargetStore, RecentTargetStore>();
        services.AddSingleton<IChannelSettingsStore, JsonChannelSettingsStore>();
        services.AddSingleton<IChannelSettingsService, ChannelSettingsService>();
        services.AddSingleton<IWebhookVerificationService, WebhookVerificationService>();
        services.AddSingleton<IChannelClient, TelegramChannel>();
        services.AddSingleton<IChannelClient, LineChannel>();
        services.AddSingleton<IChannelClient, EmailChannel>();
        services.AddSingleton<IChannelRegistry, ChannelRegistry>();
        services.AddSingleton<IMessageOrchestrator, MessageOrchestrator>();
        return services;
    }
}
