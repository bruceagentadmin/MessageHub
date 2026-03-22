using MessageHub.Core;
using Microsoft.Extensions.DependencyInjection;

namespace MessageHub.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddMessageHubInfrastructure(this IServiceCollection services)
    {
        // Polly 重試管線 (3 次指數退避)
        services.AddSingleton<IRetryPipeline, PollyRetryPipeline>();

        return services;
    }
}
