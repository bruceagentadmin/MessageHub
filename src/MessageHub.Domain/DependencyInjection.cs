using MessageHub.Domain.Services;
using Microsoft.Extensions.DependencyInjection;

namespace MessageHub.Domain;

public static class DependencyInjection
{
    public static IServiceCollection AddMessageHubDomain(this IServiceCollection services)
    {
        services.AddSingleton<IMessagingService, MessagingService>();
        services.AddSingleton<IHistoryService, HistoryService>();
        services.AddSingleton<IContactService, ContactService>();
        return services;
    }
}
