using MessageHub.Core.Bus;
using MessageHub.Core.Channels;
using MessageHub.Core.Services;
using MessageHub.Core.Stores;
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
    /// 包含：儲存層、頻道實作、訊息匯流排、業務服務、通知服務、Webhook 驗證，以及背景 Worker。
    /// </summary>
    /// <param name="services">要加入服務的 <see cref="IServiceCollection"/> 實例。</param>
    /// <returns>相同的 <see cref="IServiceCollection"/> 實例，支援鏈式呼叫。</returns>
    public static IServiceCollection AddMessageHubCore(this IServiceCollection services)
    {
        // ─── 儲存層（Stores） ────────────────────────────────────────────────
        // 各 Store 均為 Singleton，整個應用程式生命週期內共享同一個實例：
        // - IMessageLogStore：記憶體內訊息日誌（最多 500 筆，服務重啟後清空）
        // - IRecentTargetStore：各頻道最後一次互動對象的記憶體快取
        // - IChannelSettingsStore：JSON 檔案持久化的頻道設定（讀寫 data/channel-settings.json）
        services.AddSingleton<IMessageLogStore, InMemoryMessageLogStore>();
        services.AddSingleton<IRecentTargetStore, RecentTargetStore>();
        services.AddSingleton<IChannelSettingsStore, JsonChannelSettingsStore>();

        // ─── 頻道實作（Channels） ────────────────────────────────────────────
        // 每個頻道以 IChannel 介面型別多次註冊 Singleton，
        // ChannelFactory 透過 IEnumerable<IChannel> 注入取得所有頻道實作。
        // ChannelFactory 本身也是 Singleton，建構時即建立頻道查找字典。
        services.AddSingleton<IChannel, TelegramChannel>();
        services.AddSingleton<IChannel, LineChannel>();
        services.AddSingleton<IChannel, EmailChannel>();
        services.AddSingleton<ChannelFactory>();

        // ─── 訊息匯流排（MessageBus） ────────────────────────────────────────
        // MessageBus 管理三條 Channel<T>：Outbound（對外發送）、Inbound（收訊處理）、DLQ（失敗佇列）。
        // 以具體型別 MessageBus 作為 Singleton 主要實例，
        // 再透過委派工廠將 IMessageBus 介面解析指向同一個 MessageBus 實例，
        // 確保所有注入點使用同一個佇列物件。
        services.AddSingleton<MessageBus>();
        services.AddSingleton<IMessageBus>(sp => sp.GetRequiredService<MessageBus>());

        // ─── 業務服務（Services） ────────────────────────────────────────────
        // ChannelSettingsService 同時實作兩個介面：
        // - IChannelSettingsService：提供頻道設定的 CRUD 操作
        // - ICommonParameterProvider：提供通用的鍵值參數查詢（供其他服務取得 ChannelConfig）
        // 以具體型別作為主要 Singleton，再以委派工廠讓兩個介面解析同一個實例，避免重複建立。
        services.AddSingleton<ChannelSettingsService>();
        services.AddSingleton<IChannelSettingsService>(sp => sp.GetRequiredService<ChannelSettingsService>());
        services.AddSingleton<ICommonParameterProvider>(sp => sp.GetRequiredService<ChannelSettingsService>());
        // EchoMessageProcessor：處理收到的入站訊息，產生回覆內容
        services.AddSingleton<IMessageProcessor, EchoMessageProcessor>();
        // MessageCoordinator：協調訊息的路由與轉發邏輯
        services.AddSingleton<IMessageCoordinator, MessageCoordinator>();

        // ─── 通知服務（Notification） ────────────────────────────────────────
        // NotificationService：負責透過 SignalR 或其他機制向前端推送即時事件通知
        services.AddSingleton<INotificationService, NotificationService>();

        // ─── Webhook 驗證服務（Webhook Verification） ───────────────────────
        // WebhookVerificationService：提供各頻道 Webhook 端點的連線驗證功能
        services.AddSingleton<IWebhookVerificationService, WebhookVerificationService>();

        // ─── 背景 Worker（Background Hosted Service） ────────────────────────
        // ChannelManager 作為 IHostedService 由 .NET 泛型主機管理其生命週期，
        // 在背景持續消費 MessageBus 的 Outbound 佇列，處理重試邏輯與死信佇列（DLQ），
        // 並依設定的速率限制控制各頻道的發送頻率。
        services.AddHostedService<ChannelManager>();

        return services;
    }
}
