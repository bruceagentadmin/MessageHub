using MessageHub.Core;
using MessageHub.Domain.Services;
using MessageHub.Domain.Stores;
using Microsoft.Extensions.DependencyInjection;

namespace MessageHub.Domain;

public static class DependencyInjection
{
    public static IServiceCollection AddMessageHubDomain(this IServiceCollection services)
    {
        // ─── 儲存層（Stores） ────────────────────────────────────────────────
        // IChannelSettingsStore：JSON 檔案持久化的頻道設定（讀寫 data/channel-settings.json）
        services.AddSingleton<IChannelSettingsStore, JsonChannelSettingsStore>();

        // ─── 頻道設定服務（Channel Settings） ────────────────────────────────
        // ChannelSettingsService 同時實作 IChannelSettingsService 與 ICommonParameterProvider
        services.AddSingleton<ChannelSettingsService>();
        services.AddSingleton<IChannelSettingsService>(sp => sp.GetRequiredService<ChannelSettingsService>());
        services.AddSingleton<ICommonParameterProvider>(sp => sp.GetRequiredService<ChannelSettingsService>());

        // ─── 通知服務（Notification） ────────────────────────────────────────
        services.AddSingleton<INotificationService, NotificationService>();

        // ─── Webhook 驗證服務（Webhook Verification） ───────────────────────
        services.AddSingleton<IWebhookVerificationService, WebhookVerificationService>();

        // ─── Domain 服務 ─────────────────────────────────────────────────────
        services.AddSingleton<IMessagingService, MessagingService>();
        services.AddSingleton<IHistoryService, HistoryService>();
        services.AddSingleton<IContactService, ContactService>();

        return services;
    }
}
