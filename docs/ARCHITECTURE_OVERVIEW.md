# MessageHub 專案架構說明

> 本文件詳述 MessageHub 專案的實際實作架構，並透過類別圖與循序圖深入說明 Bus 傳輸機制與 Channel 實作關係。

## 1. 專案分層結構 (Project Layers)

本專案採用典型的乾淨架構 (Clean Architecture) 原則：

- **MessageHub.Core**: 核心層。定義所有介面 (Interfaces)、模型 (Models)，並包含通訊相關實作：頻道 (Channels/)、訊息匯流排 (Bus/)、訊息協調與處理 (Services/)。Core 定義 `IRetryPipeline` 介面抽象化重試邏輯，不直接依賴任何重試框架。
- **MessageHub.Domain**: 領域層。包含頻道設定服務 (ChannelSettingsService)、通知服務 (NotificationService)、Webhook 驗證 (WebhookVerificationService)、JSON 設定儲存 (JsonChannelSettingsStore)，以及領域服務 (MessagingService, HistoryService, ContactService)。參照 Core。
- **MessageHub.Worker**: 背景服務層。包含 ChannelManager (BackgroundService)，負責消費 Outbound 佇列、重試、限流、Dead Letter Queue 處理。參照 Core。
- **MessageHub.Infrastructure**: 基礎設施層。提供 `IRetryPipeline` 的 Polly 實作及 SQLite 儲存實作。參照 Core + Domain。
- **MessageHub.Api**: 外部接口層。ASP.NET Core Controllers。

---

## 2. 核心類別圖 (Core Class Diagram)

以下依功能特性拆分為四組類別圖，各圖聚焦單一職責以降低閱讀複雜度。

### 2.1 訊息模型與租戶設定 (Data Models & Configuration)

系統中流動的三種訊息物件，以及多租戶頻道設定結構。

```mermaid
classDiagram
    direction LR

    class OutboundMessage {
        <<record>>
        +TenantId: string
        +Channel: string
        +ChatId: string
        +Content: string
        +Metadata: object?
    }

    class InboundMessage {
        <<record>>
        +TenantId: string
        +Channel: string
        +ChatId: string
        +SenderId: string
        +Content: string
        +ReceivedAt: DateTimeOffset
        +OriginalPayload: string?
    }

    class DeadLetterMessage {
        <<record>>
        +Original: OutboundMessage
        +Reason: string
        +RetryCount: int
        +FailedAt: DateTimeOffset
    }

    class ChannelConfig {
        +TenantId: Guid
        +Channels: Dictionary~string, ChannelSettings~
    }

    class ChannelSettings {
        +Enabled: bool
        +Parameters: Dictionary~string, string~
    }

    class ICommonParameterProvider {
        <<interface>>
        +GetParameterByKeyAsync~T~(key, ct) T
    }

    class IChannelSettingsService {
        <<interface>>
        +GetAsync(ct) ChannelConfig
        +SaveAsync(config, ct) ChannelConfig
        +GetChannelTypes() IReadOnlyList~ChannelTypeDefinition~
        +GetSettingsFilePath() string
    }

    DeadLetterMessage --> OutboundMessage : 包裝失敗訊息
    ChannelConfig o-- ChannelSettings : 字典聚合 (key=頻道名)
    ICommonParameterProvider <|-- IChannelSettingsService : 繼承
    IChannelSettingsService ..> ChannelConfig : 讀寫
```

> **設計要點**：`ChannelConfig.Channels` 採 `Dictionary<string, ChannelSettings>` 結構，key 直接對應頻道名稱（如 `"line"`、`"telegram"`），與 JSON 設定檔的結構一致。`DeadLetterMessage` 包裝原始 `OutboundMessage` 加上失敗原因與重試次數，供 DLQ 消費端追蹤。

### 2.2 頻道系統 (Channel System)

`IChannel` 介面定義所有頻道的統一行為，`ChannelFactory` 負責依名稱路由至正確實例。

```mermaid
classDiagram
    direction TB

    class IChannel {
        <<interface>>
        +Name: string
        +ParseRequestAsync(tenantId, request, ct) InboundMessage
        +SendAsync(chatId, message, settings, ct) Task
    }

    class ChannelFactory {
        -Dictionary~string, IChannel~ _lookup
        +GetChannel(name) IChannel
        +GetDefinitions() IReadOnlyList~ChannelDefinition~
    }

    class LineChannel {
        -IChannelSettingsService _settings
        -HttpClient _httpClient
        +Name: "line"
    }

    class TelegramChannel {
        -IChannelSettingsService _settings
        -HttpClient _httpClient
        +Name: "telegram"
    }

    class EmailChannel {
        <<internal>>
        -IChannelSettingsService _settings
        +Name: "email"
    }

    IChannel <|.. LineChannel : 實作
    IChannel <|.. TelegramChannel : 實作
    IChannel <|.. EmailChannel : 實作
    ChannelFactory o-- IChannel : 聚合 (name → instance)

    LineChannel --> HttpClient : HTTP 呼叫
    TelegramChannel --> HttpClient : HTTP 呼叫
```

> **設計要點**：各 Channel 直接實作 `IChannel`（無中間抽象基底類別）。`SendAsync` 回傳 `Task`，失敗時拋出例外而非回傳錯誤物件 — 日誌記錄責任統一由 `ChannelManager` 承擔。新增頻道僅需實作 `IChannel` 並在 DI 註冊，不需修改任何核心邏輯。

### 2.3 訊息匯流排與分發引擎 (Message Bus & Channel Manager)

`IMessageBus` 提供三條佇列（Outbound / Inbound / DLQ），`ChannelManager`（位於 MessageHub.Worker 命名空間）作為背景 Worker 消費 Outbound 並透過 `IRetryPipeline` 整合重試、限流與死信處理。Core 定義 `IRetryPipeline` 介面抽象化重試邏輯，Infrastructure 提供 Polly 實作。

```mermaid
classDiagram
    direction TB

    class IMessageBus {
        <<interface>>
        +PublishOutboundAsync(message, ct) ValueTask
        +ConsumeOutboundAsync(ct) IAsyncEnumerable~OutboundMessage~
        +PublishInboundAsync(message, ct) ValueTask
        +ConsumeInboundAsync(ct) IAsyncEnumerable~InboundMessage~
        +PublishDeadLetterAsync(message, ct) ValueTask
        +ConsumeDeadLetterAsync(ct) IAsyncEnumerable~DeadLetterMessage~
    }

    class MessageBus {
        <<internal>>
        -Channel~OutboundMessage~ _outbound
        -Channel~InboundMessage~ _inbound
        -Channel~DeadLetterMessage~ _deadLetter
        +OutboundPendingCount: int
        +InboundPendingCount: int
        +DeadLetterPendingCount: int
    }

    class IRetryPipeline {
        <<interface>>
        +ExecuteAsync(action, ct) Task
    }

    class PollyRetryPipeline {
        <<Infrastructure>>
        -ResiliencePipeline Pipeline
        +ExecuteAsync(action, ct) Task
    }

    class ChannelManager {
        <<Worker, BackgroundService>>
        -IRetryPipeline _retryPipeline
        -ConcurrentDictionary~string, SemaphoreSlim~ _rateLimiters
        #ExecuteAsync(stoppingToken) Task
        -ProcessMessageAsync(message, ct) Task
    }

    IMessageBus <|.. MessageBus : 實作 (System.Threading.Channels)
    IRetryPipeline <|.. PollyRetryPipeline : 實作 (Polly)

    ChannelManager --> IMessageBus : ConsumeOutbound / PublishDeadLetter
    ChannelManager --> IRetryPipeline : 重試策略委派
    ChannelManager --> ChannelFactory : GetChannel(name)
    ChannelManager --> IChannelSettingsService : 取得頻道設定
    ChannelManager --> IMessageLogStore : 記錄成功/失敗日誌
```

> **設計要點**：
> - **重試抽象**：Core 定義 `IRetryPipeline` 介面，`ChannelManager` 透過注入的 `IRetryPipeline` 執行重試，不直接依賴 Polly。Infrastructure 提供 `PollyRetryPipeline`（3 次指數退避 1s → 2s → 4s）。
> - **Dead Letter Queue**：重試耗盡後，將 `DeadLetterMessage` 發布至 DLQ 通道，並記錄 Failed 日誌。
> - **Per-Channel 限流**：`ConcurrentDictionary<string, SemaphoreSlim>` 確保同一頻道的發送不超過平台限流閥值。
> - **Inbound 通道**：目前預留，未來可用於異步進站處理。
> - **監控**：`MessageBus` 暴露 `OutboundPendingCount`、`InboundPendingCount`、`DeadLetterPendingCount` 作為效能指標。
> - **封裝性**：`ChannelManager` 與 `MessageBus` 皆為 `internal` 類別，僅透過 DI 與介面對外暴露，降低公開 API 表面積。

### 2.4 業務入口與訊息推送 (Business Entry Points)

`MessageCoordinator` 與 `NotificationService` 作為業務邏輯的兩個入口，皆透過 `IMessageBus.PublishOutboundAsync` 推送訊息，實現「發後即忘」的解耦模式。

```mermaid
classDiagram
    direction TB

    class IMessageProcessor {
        <<interface>>
        +ProcessAsync(message, ct) string
    }

    class MessageCoordinator {
        -IMessageLogStore _logStore
        -ChannelFactory _channelFactory
        -IRecentTargetStore _recentTargetStore
        -IMessageBus _messageBus
        +HandleInboundAsync(tenantId, channel, request, ct) MessageLogEntry
        +SendManualAsync(request, ct) MessageLogEntry
        +ProcessAsync(message, ct) string
    }

    class INotificationService {
        <<interface>>
        +SendNotificationAsync(tenantId, channel, msg, ct) Task
    }

    class NotificationService {
        <<Domain>>
        -ChannelFactory _channelFactory
        -IChannelSettingsService _settings
        -IMessageBus _messageBus
    }

    IMessageProcessor <|.. MessageCoordinator : 實作
    INotificationService <|.. NotificationService : 實作

    MessageCoordinator --> IMessageBus : PublishOutboundAsync
    MessageCoordinator --> ChannelFactory : ParseRequestAsync (進站解析)
    MessageCoordinator --> IMessageLogStore : 記錄日誌
    MessageCoordinator --> IRecentTargetStore : 追蹤最近互動對象

    NotificationService --> IMessageBus : PublishOutboundAsync
    NotificationService --> ChannelFactory : 驗證頻道存在
    NotificationService --> IChannelSettingsService : 取得 NotificationTargetId
```

> **設計要點**：
> - **MessageCoordinator**：處理 Webhook 進站（`HandleInboundAsync`）與手動發送（`SendManualAsync`），兩者最終皆透過 Bus 推送，不直接呼叫 `IChannel.SendAsync`。
> - **NotificationService**：從 `ChannelSettings.Parameters` 取得 `NotificationTargetId`，組裝 `OutboundMessage` 後推送至 Bus，實現排程通知的「發後即忘」。
> - 兩者皆不持有 `IChannel` 的發送責任 — 實際發送由 `ChannelManager`（§2.3）背景消費處理。

---

## 3. 重要流程說明 (Sequence Diagrams)

### 3.1 進站訊息與異步回覆 (Inbound → Bus → Outbound)

Webhook 收到訊息後，經 `UnifiedMessageProcessor` 處理，回覆推送至 MessageBus 異步發送。

```mermaid
sequenceDiagram
    autonumber
    participant User as 使用者
    participant Platform as 外部平台 (Line/TG)
    participant WC as WebhookController
    participant MC as MessageCoordinator
    participant CH as IChannel (ParseRequest)
    participant Store as IMessageLogStore
    participant Bus as IMessageBus
    participant CM as ChannelManager (Worker)
    participant Send as IChannel (SendAsync)

    User->>Platform: 發送訊息
    Platform->>WC: Webhook POST
    WC->>MC: HandleInboundAsync(tenantId, channel, request)
    MC->>CH: ParseRequestAsync(request)
    CH-->>MC: InboundMessage
    MC->>Store: AddAsync(inboundLog)
    MC->>MC: ProcessAsync(inbound) → 回覆文字
    MC->>Bus: PublishOutboundAsync(OutboundMessage)
    MC-->>WC: inboundLog
    WC-->>Platform: HTTP 200 OK (立即響應)

    Note over CM: 背景迴圈監聽中...
    CM->>Bus: ConsumeOutboundAsync()
    Bus-->>CM: OutboundMessage
    CM->>CM: IRetryPipeline 重試 + 限流控制
    CM->>Send: SendAsync(chatId, message, settings)
    Send->>Platform: API Call
    Platform->>User: 顯示回覆
    CM->>Store: AddAsync(successLog)
```

#### 3.1.1 失敗處理流程 (Dead Letter Queue)

```mermaid
sequenceDiagram
    autonumber
    participant CM as ChannelManager
    participant CH as IChannel
    participant Bus as IMessageBus (DLQ)
    participant Store as IMessageLogStore

    CM->>CH: SendAsync (第 1 次)
    CH--xCM: Exception
    CM->>CH: SendAsync (第 2 次, Polly 重試)
    CH--xCM: Exception
    CM->>CH: SendAsync (第 3 次, Polly 重試)
    CH--xCM: Exception

    Note over CM: 3 次重試皆失敗
    CM->>Bus: PublishDeadLetterAsync(DeadLetterMessage)
    CM->>Store: AddAsync(failedLog)
```

### 3.2 系統主動通知 (Notification → Bus → Channel)

```mermaid
sequenceDiagram
    autonumber
    participant Cron as 定時排程器
    participant NS as NotificationService
    participant Bus as IMessageBus
    participant CM as ChannelManager (Worker)
    participant CF as ChannelFactory
    participant CH as IChannel
    participant User as 使用者

    Cron->>NS: 觸發通知任務
    NS->>NS: 從 ChannelSettings 取得 NotificationTargetId
    NS->>Bus: PublishOutboundAsync(OutboundMessage)
    Note over Bus: 發後即忘，NS 立即返回

    CM->>Bus: ConsumeOutboundAsync()
    Bus-->>CM: OutboundMessage
    CM->>CF: GetChannel(message.Channel)
    CF-->>CM: IChannel 實例
    CM->>CH: SendAsync(chatId, message, settings)
    CH-->>User: 顯示通知訊息
```

### 3.3 手動發送 (ControlCenter → Bus → Channel)

```mermaid
sequenceDiagram
    autonumber
    participant UI as 使用者介面
    participant CC as ControlCenterController
    participant MC as MessageCoordinator
    participant Bus as IMessageBus
    participant Store as IMessageLogStore
    participant CM as ChannelManager (Worker)
    participant CH as IChannel

    UI->>CC: POST /api/control/send
    CC->>MC: SendManualAsync(request)
    MC->>Bus: PublishOutboundAsync(OutboundMessage)
    MC->>Store: AddAsync(pendingLog)
    MC-->>CC: pendingLog
    CC-->>UI: 200 OK

    CM->>Bus: ConsumeOutboundAsync()
    Bus-->>CM: OutboundMessage
    CM->>CH: SendAsync(chatId, message, settings)
```

---

## 4. 異步機制與技術細節 (Implementation Details)

| 技術點 | 實作方式 |
|---|---|
| **訊息匯流排** | `System.Threading.Channels`：`Channel.CreateUnbounded<T>()` 三條佇列 (Outbound/Inbound/DLQ) |
| **頻道註冊** | DI 容器 `AddSingleton<IChannel, XxxChannel>`，`ChannelFactory` 透過 `IEnumerable<IChannel>` 注入建立 name→instance 對照表 |
| **重試機制** | Core 定義 `IRetryPipeline` 介面，Infrastructure 提供 `PollyRetryPipeline`（Polly `ResiliencePipeline`：3 次重試、指數退避 1s → 2s → 4s） |
| **死信佇列** | 重試 3 次仍失敗 → `PublishDeadLetterAsync(DeadLetterMessage)` |
| **限流控制** | `ConcurrentDictionary<string, SemaphoreSlim>` 每頻道一把鎖，確保不超過平台限流閥值 |
| **多租戶** | `OutboundMessage` 含 `TenantId`，`ChannelManager` 透過 `IChannelSettingsService` 動態取得對應租戶設定 |
| **監控指標** | `MessageBus.OutboundPendingCount`、`InboundPendingCount`、`DeadLetterPendingCount` |

---

## 5. 設計決策摘要

1. **訊息解耦 (Fire and Forget)**: 所有發送動作皆透過 `IMessageBus.PublishOutboundAsync` 推送，業務層不需等待外部 API 響應。`WebhookController`、`NotificationService`、`SendManualAsync` 均遵循此模式。
2. **工廠模式**: `ChannelFactory` 隔離具體頻道實作。新增頻道（如 Slack, SMS）僅需實作 `IChannel` 並在 DI 註冊即可，不需修改核心邏輯。
3. **多租戶支持**: 資料模型皆包含 `TenantId`，透過 `IChannelSettingsService` 動態載入各租戶的 API Key 與 Token。`ChannelConfig.Channels` 採 `Dictionary<string, ChannelSettings>` 結構，key 為頻道名稱（如 `"line"`、`"telegram"`）。
4. **Channel 職責單純化**: `IChannel.SendAsync` 回傳 `Task`（而非 `Task<MessageLogEntry>`），失敗時拋出例外。日誌記錄由 `ChannelManager` 統一負責，避免各 Channel 分散處理。
5. **彈性與容錯**: `ChannelManager` 透過 `IRetryPipeline` 介面整合重試 + Dead Letter Queue + Per-Channel 限流，三道防線確保生產環境穩定性。Core 不直接依賴 Polly，可在 Infrastructure 層替換為任何重試策略。
6. **封裝性 (Internal 策略)**: `ChannelManager` 在 Worker 中為 `internal`。`NotificationService`、`WebhookVerificationService`、`JsonChannelSettingsStore` 在 Domain 中為 `internal`。`MessageBus`、`EmailChannel` 在 Core 中為 `internal`。其餘實作類別透過介面與 DI 對外暴露，降低公開 API 表面積。Tests 透過 `InternalsVisibleTo` 存取 internal 類別。

---

## 6. 設定檔結構 (JSON Configuration)

存於租戶設定的 `ConfigsJson` 欄位，`ChannelConfig.Channels` 為字典結構：

```json
{
  "channels": {
    "line": {
      "enabled": true,
      "parameters": {
        "ChannelAccessToken": "...",
        "ChannelSecret": "...",
        "NotificationTargetId": "U12345678..."
      }
    },
    "telegram": {
      "enabled": true,
      "parameters": {
        "BotToken": "...",
        "NotificationTargetId": "-1001234567"
      }
    },
    "email": {
      "enabled": false,
      "parameters": {
        "SmtpHost": "...",
        "SmtpPort": "587"
      }
    }
  }
}
```

---

## 7. 類別清單 (Class Inventory)

### 7.1 核心介面 (MessageHub.Core)

| 介面 | 用途 |
|---|---|
| `IChannel` | 頻道通用介面。Name、ParseRequestAsync、SendAsync(chatId, message, settings) → Task |
| `IMessageBus` | 訊息匯流排。Outbound / Inbound / DLQ 三組 Publish + Consume |
| `IMessageProcessor` | 訊息處理入口。ProcessAsync(InboundMessage) → string |
| `INotificationService` | 通知服務。SendNotificationAsync 推送至 Bus |
| `ICommonParameterProvider` | 參數提供者。GetParameterByKeyAsync\<T\>(key) |
| `IChannelSettingsService` | 頻道設定服務（繼承 ICommonParameterProvider）。Get/Save/GetChannelTypes |
| `IChannelSettingsStore` | 頻道設定儲存層介面 |
| `IMessageLogStore` | 訊息紀錄儲存介面。AddAsync / GetRecentAsync |
| `IRecentTargetStore` | 最近互動對象儲存介面 |
| `IRetryPipeline` | 重試管線介面。ExecuteAsync(action, ct) → Task |
| `IWebhookVerificationService` | Webhook 驗證服務介面 |

### 7.2 資料模型 (MessageHub.Core/Models)

| 模型 | 型別 | 欄位 |
|---|---|---|
| `OutboundMessage` | record | TenantId, Channel, ChatId, Content, Metadata? |
| `InboundMessage` | record | TenantId, Channel, ChatId, SenderId, Content, ReceivedAt, OriginalPayload? |
| `DeadLetterMessage` | record | Original (OutboundMessage), Reason, RetryCount, FailedAt |
| `ChannelConfig` | class | TenantId (Guid), Channels (Dictionary\<string, ChannelSettings\>) |
| `ChannelSettings` | class | Enabled (bool), Parameters (Dictionary\<string, string\>) |
| `MessageLogEntry` | record | Id, Timestamp, TenantId, Channel, Direction, Status, TargetId, Content, Source, Details? |
| `SendMessageRequest` | record | TenantId, Channel, TargetId, Content, TriggeredBy? |
| `WebhookTextMessageRequest` | record | ChatId, SenderId, Content |
| `DeliveryStatus` | enum | Pending, Delivered, Failed |
| `MessageDirection` | enum | Inbound, Outbound, System |

### 7.3 業務服務實作 (MessageHub.Core/Services)

| 類別 | 用途 |
|---|---|
| `MessageCoordinator` | 實作 IMessageCoordinator。HandleInboundAsync 處理 Webhook → Bus、SendManualAsync 手動發送 → Bus |
| `EchoMessageProcessor` | 實作 IMessageProcessor。產生回覆文字 |

### 7.4 頻道實作 (MessageHub.Core/Channels)

| 類別 | 可見性 | 用途 |
|---|---|---|
| `LineChannel` | public | 實作 IChannel。Line Messaging API (Push) |
| `TelegramChannel` | public | 實作 IChannel。Telegram Bot API (sendMessage) |
| `EmailChannel` | internal | 實作 IChannel。Email 模擬通道 (POC) |

### 7.5 訊息匯流排 (MessageHub.Core/Bus)

| 類別 | 可見性 | 用途 |
|---|---|---|
| `MessageBus` | internal | 實作 IMessageBus。三條 `System.Threading.Channels` 佇列 + 監控計數 |

### 7.6 領域服務實作 (MessageHub.Domain)

| 類別 | 可見性 | 用途 |
|---|---|---|
| `ChannelSettingsService` | public | 實作 IChannelSettingsService + ICommonParameterProvider |
| `JsonChannelSettingsStore` | internal | 實作 IChannelSettingsStore (JSON 檔案) |
| `NotificationService` | internal | 實作 INotificationService。取得 NotificationTargetId → PublishOutboundAsync |
| `WebhookVerificationService` | internal | 實作 IWebhookVerificationService |
| `MessagingService` | public | 提供發送訊息與對象解析相關領域邏輯 |
| `HistoryService` | public | 提供訊息紀錄查詢領域邏輯 |
| `ContactService` | public | 提供最近互動對象相關領域邏輯 |

### 7.7 背景服務層 (MessageHub.Worker)

| 類別 | 可見性 | 用途 |
|---|---|---|
| `ChannelManager` | internal | BackgroundService。消費 Outbound → IRetryPipeline 重試 → 限流 → DLQ → 日誌 |

### 7.8 基礎設施層實作 (MessageHub.Infrastructure)

| 類別 | 用途 |
|---|---|
| `PollyRetryPipeline` | 實作 IRetryPipeline。Polly ResiliencePipeline：3 次指數退避重試 (1s → 2s → 4s) |
| `SqliteMessageLogStore` | 實作 IMessageLogStore (SQLite) |

### 7.9 API 層 (MessageHub.Api)

| Controller | 路由 | 用途 |
|---|---|---|
| `TelegramWebhookController` | POST /api/telegram/webhook | 解析 Telegram Update → HandleInboundAsync |
| `LineWebhookController` | POST /api/line/webhook | 解析 Line Events → HandleInboundAsync |
| `ControlCenterController` | /api/control/* | 手動發送、查看日誌、管理設定 |
| `WebhooksController` | /api/webhooks/* | 通用 Webhook 文字訊息接收端點（適合 Postman 測試） |

---

## 8. DI 註冊 (DependencyInjection)

本系統採用分層 DI 註冊，由 `Program.cs` 呼叫各層的擴充方法：

```csharp
// MessageHub.Core — 僅註冊通訊相關服務
public static IServiceCollection AddMessageHubCore(this IServiceCollection services)
{
    // Channels
    services.AddSingleton<IChannel, TelegramChannel>();
    services.AddSingleton<IChannel, LineChannel>();
    services.AddSingleton<IChannel, EmailChannel>();
    services.AddSingleton<ChannelFactory>();

    // MessageBus
    services.AddSingleton<MessageBus>();
    services.AddSingleton<IMessageBus>(sp => sp.GetRequiredService<MessageBus>());

    // Services
    services.AddSingleton<IMessageProcessor, EchoMessageProcessor>();
    services.AddSingleton<IMessageCoordinator, MessageCoordinator>();

    return services;
}

// MessageHub.Domain — 頻道設定、通知、Webhook 驗證、領域服務
public static IServiceCollection AddMessageHubDomain(this IServiceCollection services)
{
    services.AddSingleton<IChannelSettingsStore, JsonChannelSettingsStore>();
    services.AddSingleton<ChannelSettingsService>();
    services.AddSingleton<IChannelSettingsService>(sp => sp.GetRequiredService<ChannelSettingsService>());
    services.AddSingleton<ICommonParameterProvider>(sp => sp.GetRequiredService<ChannelSettingsService>());
    services.AddSingleton<INotificationService, NotificationService>();
    services.AddSingleton<IWebhookVerificationService, WebhookVerificationService>();
    services.AddSingleton<IMessagingService, MessagingService>();
    services.AddSingleton<IHistoryService, HistoryService>();
    services.AddSingleton<IContactService, ContactService>();
    return services;
}

// MessageHub.Worker — 背景分發引擎
public static IServiceCollection AddMessageHubWorker(this IServiceCollection services)
{
    services.AddHostedService<ChannelManager>();
    return services;
}

// MessageHub.Infrastructure — Polly 重試管線 + SQLite 儲存
public static IServiceCollection AddMessageHubInfrastructure(this IServiceCollection services)
{
    services.AddSingleton<IRetryPipeline, PollyRetryPipeline>();
    return services;
}
```

---

## 9. 實作路徑參考

| 類別 | 路徑 |
|---|---|
| 核心介面 | `src/MessageHub.Core/` (I*.cs) |
| 資料模型 | `src/MessageHub.Core/Models/` |
| 業務服務 | `src/MessageHub.Core/Services/` |
| 頻道實作 | `src/MessageHub.Core/Channels/` |
| 訊息匯流排 | `src/MessageHub.Core/Bus/` |
| 頻道設定服務 | `src/MessageHub.Domain/Services/ChannelSettingsService.cs` |
| 通知服務 | `src/MessageHub.Domain/Services/NotificationService.cs` |
| Webhook 驗證 | `src/MessageHub.Domain/Services/WebhookVerificationService.cs` |
| JSON 設定儲存 | `src/MessageHub.Domain/Stores/JsonChannelSettingsStore.cs` |
| Domain DI | `src/MessageHub.Domain/DependencyInjection.cs` |
| ChannelManager | `src/MessageHub.Worker/ChannelManager.cs` |
| Worker DI | `src/MessageHub.Worker/DependencyInjection.cs` |
| 重試管線 (Polly) | `src/MessageHub.Infrastructure/PollyRetryPipeline.cs` |
| Infrastructure DI | `src/MessageHub.Infrastructure/DependencyInjection.cs` |
| API 進入點 | `src/MessageHub.Api/Controllers/` |
| 測試 | `tests/MessageHub.Tests/` |
