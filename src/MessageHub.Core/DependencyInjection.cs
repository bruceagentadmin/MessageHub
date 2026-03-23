using MessageHub.Core.Bus;
using MessageHub.Core.Channels;
using MessageHub.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace MessageHub.Core;

/// <summary>
/// MessageHub.Core 模組的相依性注入擴充方法集合。
/// 提供 <see cref="AddMessageHubCore"/> 擴充方法，供宿主應用程式（如 MessageHub.Api）
/// 一次性完成 Core 層所有服務的 DI 容器註冊。
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// 將 MessageHub Core 層的所有服務註冊至 <see cref="IServiceCollection"/>。
    /// 包含：頻道實作、訊息匯流排、訊息協調器、訊息處理器。
    /// </summary>
    /// <param name="services">要加入服務的 <see cref="IServiceCollection"/> 實例。</param>
    /// <returns>相同的 <see cref="IServiceCollection"/> 實例，支援鏈式呼叫。</returns>
    public static IServiceCollection AddMessageHubCore(this IServiceCollection services)
    {
        // ─── 頻道實作（Channels） ────────────────────────────────────────────
        services.AddSingleton<IChannel, TelegramChannel>();
        services.AddSingleton<IChannel, LineChannel>();
        services.AddSingleton<IChannel, EmailChannel>();
        services.AddSingleton<ChannelFactory>();

        // ─── 訊息匯流排（MessageBus） ────────────────────────────────────────
        services.AddSingleton<MessageBus>();
        services.AddSingleton<IMessageBus>(sp => sp.GetRequiredService<MessageBus>());

        // ─── 業務服務（Services） ────────────────────────────────────────────
        services.AddSingleton<IMessageProcessor, EchoMessageProcessor>();
        services.AddSingleton<IMessageCoordinator, MessageCoordinator>();

        return services;
    }
}
