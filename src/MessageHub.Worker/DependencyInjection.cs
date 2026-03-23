using Microsoft.Extensions.DependencyInjection;

namespace MessageHub.Worker;

/// <summary>
/// MessageHub.Worker 模組的相依性注入擴充方法集合。
/// 註冊背景工作服務（ChannelManager）。
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// 將 MessageHub Worker 層的服務註冊至 <see cref="IServiceCollection"/>。
    /// 包含：ChannelManager 背景服務（消費 Outbound 佇列並分發至各頻道）。
    /// </summary>
    public static IServiceCollection AddMessageHubWorker(this IServiceCollection services)
    {
        services.AddHostedService<ChannelManager>();
        return services;
    }
}
