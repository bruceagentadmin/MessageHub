# 07 — Dependency Injection 相依性注入

> 本文件詳述 `DependencyInjection.cs` 中的完整服務註冊架構。

---

## 入口方法

```csharp
public static IServiceCollection AddMessageHubCore(this IServiceCollection services)
```

宿主應用程式（`MessageHub.Api`）只需一行呼叫即可完成 Core 層所有服務的 DI 註冊：

```csharp
builder.Services.AddMessageHubCore();
```

---

## 完整服務註冊對照表

### Core 層（`AddMessageHubCore`）— 共 15 筆註冊

| # | 介面 / 型別 | 實作 | 生命週期 | 說明 |
|---|-------------|------|---------|------|
| 1 | `IChannelSettingsStore` | `JsonChannelSettingsStore` | Singleton | JSON 檔案持久化設定 |
| 2 | `IChannel` | `TelegramChannel` | Singleton | Telegram 頻道 |
| 3 | `IChannel` | `LineChannel` | Singleton | LINE 頻道 |
| 4 | `IChannel` | `EmailChannel` | Singleton | Email 頻道（POC no-op）|
| 5 | `ChannelFactory` | `ChannelFactory` | Singleton | 頻道查找工廠 |
| 6 | `MessageBus`（具體型別）| `MessageBus` | Singleton | 佇列主實例 |
| 7 | `IMessageBus` | → `MessageBus`（委派）| Singleton | 介面指向同一實例 |
| 8 | `ChannelSettingsService`（具體型別）| `ChannelSettingsService` | Singleton | 設定服務主實例 |
| 9 | `IChannelSettingsService` | → `ChannelSettingsService`（委派）| Singleton | 介面指向同一實例 |
| 10 | `ICommonParameterProvider` | → `ChannelSettingsService`（委派）| Singleton | 介面指向同一實例 |
| 11 | `IMessageProcessor` | `EchoMessageProcessor` | Singleton | POC 回覆處理器 |
| 12 | `IMessageCoordinator` | `MessageCoordinator` | Singleton | 訊息協調器 |
| 13 | `INotificationService` | `NotificationService` | Singleton | 主動通知服務 |
| 14 | `IWebhookVerificationService` | `WebhookVerificationService` | Singleton | Webhook 驗證 |
| 15 | `IHostedService` | `ChannelManager` | HostedService | 背景佇列消費者 |

### Infrastructure 層（`AddMessageHubInfrastructure`）— 儲存與重試

| # | 介面 / 型別 | 實作 | 生命週期 | 說明 |
|---|-------------|------|---------|------|
| 1 | `IRetryPipeline` | `PollyRetryPipeline` | Singleton | Polly 3 次指數退避重試 |
| 2 | `SqliteConnectionFactory` | `SqliteConnectionFactory` | Singleton | SQLite 連線工廠 |
| 3 | `SqliteMessageLogRepository`（具體型別）| `SqliteMessageLogRepository` | Singleton | 日誌儲存主實例 |
| 4 | `IMessageLogStore` | → `SqliteMessageLogRepository`（委派）| Singleton | Core 介面指向 SQLite 實作 |
| 5 | `IMessageLogRepository` | → `SqliteMessageLogRepository`（委派）| Singleton | Domain 介面指向同一實例 |
| 6 | `IRecentTargetStore` | `SqliteRecentTargetStore` | Singleton | SQLite 最近互動目標 |
| 7 | `IContactRepository` | `SqliteContactRepository` | Singleton | SQLite 聯絡人儲存 |

> **注意**：`IMessageLogStore` 與 `IRecentTargetStore` 原先由 Core 層以記憶體實作（`InMemoryMessageLogStore`、`RecentTargetStore`）註冊，現已移至 Infrastructure 層改用 SQLite 持久化。Core 層仍保留記憶體實作原始碼供測試或備用。

---

## 註冊架構圖

```mermaid
graph TB
    subgraph "Core — Stores（儲存層）"
        S3[JsonChannelSettingsStore]
    end

    subgraph "Infrastructure — Stores（持久化）"
        IS1[SqliteMessageLogRepository]
        IS2[SqliteRecentTargetStore]
    end

    subgraph "Channels（頻道）"
        C1[TelegramChannel]
        C2[LineChannel]
        C3[EmailChannel]
        CF[ChannelFactory]
    end

    subgraph "Bus（匯流排）"
        MB[MessageBus]
        CM[ChannelManager]
    end

    subgraph "Services（業務服務）"
        CSS[ChannelSettingsService]
        EMP[EchoMessageProcessor]
        MC[MessageCoordinator]
    end

    subgraph "輔助服務"
        NS[NotificationService]
        WVS[WebhookVerificationService]
    end

    CF --> C1
    CF --> C2
    CF --> C3

    CM --> MB
    CM --> CF
    CM --> CSS
    CM --> IS1

    MC --> CF
    MC --> MB
    MC --> EMP
    MC --> IS1
    MC --> IS2

    CSS --> S3

    NS --> CF
    NS --> CSS
    NS --> MB

    WVS --> CSS
```

---

## 關鍵設計模式

### 多介面 Singleton 共用實例

當一個類別實作多個介面時，使用委派工廠確保所有介面解析到同一個實例：

```csharp
// MessageBus：具體型別 + 介面指向同一實例
services.AddSingleton<MessageBus>();
services.AddSingleton<IMessageBus>(sp => sp.GetRequiredService<MessageBus>());

// ChannelSettingsService：一個實例同時滿足兩個介面
services.AddSingleton<ChannelSettingsService>();
services.AddSingleton<IChannelSettingsService>(sp => sp.GetRequiredService<ChannelSettingsService>());
services.AddSingleton<ICommonParameterProvider>(sp => sp.GetRequiredService<ChannelSettingsService>());
```

**為什麼不直接 `services.AddSingleton<IMessageBus, MessageBus>()`？**

因為 `ChannelManager` 需要注入具體型別 `MessageBus` 來存取監控屬性（`OutboundPendingCount` 等），如果直接註冊介面，則無法注入具體型別。

### 多實作 IChannel 註冊

```csharp
services.AddSingleton<IChannel, TelegramChannel>();
services.AddSingleton<IChannel, LineChannel>();
services.AddSingleton<IChannel, EmailChannel>();
```

DI 容器會收集所有 `IChannel` 註冊，`ChannelFactory` 透過 `IEnumerable<IChannel>` 注入取得全部實作。

### HostedService 註冊

```csharp
services.AddHostedService<ChannelManager>();
```

`ChannelManager` 的生命週期由 .NET 泛型主機管理：
- 應用程式啟動時呼叫 `StartAsync` → `ExecuteAsync` 開始監聽佇列
- 應用程式關閉時觸發 `CancellationToken` → `ExecuteAsync` 的 foreach 迴圈自然結束

---

## 全部 Singleton 的設計考量

所有服務均註冊為 **Singleton**，原因：

1. **持久化儲存**：`SqliteMessageLogRepository`、`SqliteRecentTargetStore` 以 SQLite 持久化，Singleton 確保連線重用
2. **佇列共享**：`MessageBus` 必須全域唯一，所有生產者/消費者共用同一組佇列
3. **HttpClient 重用**：頻道實作內建 `HttpClient`，避免 Socket 耗盡
4. **設定快取**：`ChannelSettingsService` 作為 Singleton 可在後續版本加入記憶體快取

**注意事項**：Singleton 意味著所有服務必須是執行緒安全的。目前的實作透過 `ConcurrentQueue`、`ConcurrentDictionary` 和 `SemaphoreSlim` 達成。

---

## 新增頻道的 DI 變更清單

若要新增一個頻道（例如 Discord）：

```csharp
// 1. 在 Channels 區塊加入新頻道
services.AddSingleton<IChannel, DiscordChannel>();

// 就這樣！ChannelFactory 會自動收集所有 IChannel 實作
```

無需修改 `ChannelFactory`、`ChannelManager` 或 `MessageCoordinator`。
