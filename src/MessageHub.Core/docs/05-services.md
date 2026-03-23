# 05 — Services 業務服務

> 本文件詳述 `Services/` 資料夾下的三個業務服務：MessageCoordinator、ChannelSettingsService、EchoMessageProcessor。

---

## 總覽

| 類別 | 實作介面 | 職責 |
|------|---------|------|
| `MessageCoordinator` | `IMessageCoordinator` | 訊息流的高層協調（進站/手動發送/日誌/頻道清單）|
| `ChannelSettingsService` | `IChannelSettingsService` + `ICommonParameterProvider` | 頻道設定 CRUD + 正規化 + 通用參數查詢 |
| `EchoMessageProcessor` | `IMessageProcessor` | POC 回覆處理器（原樣回傳確認文字）|

---

## MessageCoordinator

### 角色定位

`MessageCoordinator` 是 **API 層與核心層之間的唯一入口**。所有 Controller 的訊息操作都透過它協調，它本身**不直接呼叫** `IChannel.SendAsync`，而是將訊息推入 `MessageBus`，由 `ChannelManager` 背景處理。

### 相依元件

```mermaid
graph LR
    MC[MessageCoordinator] --> CF[ChannelFactory]
    MC --> BUS[IMessageBus]
    MC --> MP[IMessageProcessor]
    MC --> LOG[IMessageLogStore]
    MC --> RTS[IRecentTargetStore]
```

### HandleInboundAsync — 進站訊息處理

處理從 Webhook 進入系統的訊息，完整流程如下：

```mermaid
sequenceDiagram
    participant API as Controller
    participant MC as MessageCoordinator
    participant CF as ChannelFactory
    participant CH as IChannel
    participant RTS as RecentTargetStore
    participant LOG as MessageLogStore
    participant MP as IMessageProcessor
    participant BUS as MessageBus

    API->>MC: HandleInboundAsync(tenantId, channel, request)

    Note over MC: Step 1: 解析 Webhook
    MC->>CF: GetChannel(channel)
    CF-->>MC: IChannel 實作
    MC->>CH: ParseRequestAsync(tenantId, request)
    CH-->>MC: InboundMessage

    Note over MC: Step 2: 記錄最近互動對象
    MC->>RTS: SetLastTargetAsync(channel, chatId, senderId)

    Note over MC: Step 3: 記錄進站日誌
    MC->>LOG: AddAsync(inboundLog)

    Note over MC: Step 4: 產生自動回覆
    MC->>MP: ProcessAsync(inbound)
    MP-->>MC: replyText

    Note over MC: Step 5: 推入 Bus（發後即忘）
    MC->>BUS: PublishOutboundAsync(outboundReply)

    MC-->>API: inboundLog
```

**重點**：Step 5 是「發後即忘」— API 回應時訊息尚未實際發送，只是排入佇列。

### SendManualAsync — 手動發送

從控制中心發起的訊息發送，包含 targetId 自動解析邏輯：

```mermaid
flowchart TD
    A[SendManualAsync] --> B[ChannelFactory.GetChannel<br/>驗證頻道存在]
    B --> C[RecentTargetStore.GetLastTargetAsync<br/>查詢最近互動對象]
    C --> D{request.TargetId<br/>有值?}
    D -- 是 --> F[使用 request.TargetId]
    D -- 否 --> E{recent.TargetId<br/>有值?}
    E -- 是 --> F2[使用 recent.TargetId]
    E -- 否 --> G[記錄失敗日誌<br/>回傳 Failed]
    F --> H[建立 OutboundMessage]
    F2 --> H
    H --> I[MessageBus.PublishOutboundAsync]
    I --> J[記錄 Pending 日誌]
    J --> K[回傳 pendingLog]
```

**TargetId 解析優先順序**：
1. `request.TargetId`（使用者明確指定）
2. `RecentTargetStore` 中該頻道的最近互動對象
3. 若兩者皆無 → 記錄 `Failed` 日誌並回傳

### GetRecentLogsAsync / GetChannels

直接委派至 `IMessageLogStore.GetRecentAsync` 與 `ChannelFactory.GetDefinitions`，無額外邏輯。

---

## ChannelSettingsService

### 雙介面實作

`ChannelSettingsService` 同時實作兩個介面，在 DI 中以同一個 Singleton 實例註冊：

```mermaid
classDiagram
    class ChannelSettingsService {
        +GetAsync() ChannelConfig
        +SaveAsync(config) ChannelConfig
        +GetChannelTypes() IReadOnlyList~ChannelTypeDefinition~
        +GetSettingsFilePath() string
        +GetParameterByKeyAsync~T~(key) T?
        -NormalizeConfig(config) ChannelConfig
        -NormalizeSettings(channelName, settings) ChannelSettings
        -Rename(parameters, oldKey, newKey)
    }

    class IChannelSettingsService {
        <<interface>>
    }
    class ICommonParameterProvider {
        <<interface>>
    }

    IChannelSettingsService <|.. ChannelSettingsService
    ICommonParameterProvider <|.. ChannelSettingsService
    ChannelSettingsService --> IChannelSettingsStore : 委派持久化
```

### 正規化流程

每次 `GetAsync` 或 `SaveAsync` 都會自動執行正規化：

```mermaid
flowchart TD
    A[NormalizeConfig] --> B[確保 Channels 不為 null]
    B --> C[過濾空白鍵值項目]
    C --> D[去除鍵名前後空白]
    D --> E[對每個頻道呼叫 NormalizeSettings]
    E --> F[重建不區分大小寫字典]

    subgraph NormalizeSettings
        G[過濾空值參數] --> H[去除參數鍵值前後空白]
        H --> I[重建不區分大小寫參數字典]
        I --> J{頻道類型?}
        J -- Line --> K["Rename: Token→ChannelAccessToken<br/>Secret→ChannelSecret"]
        J -- Telegram --> L["Rename: Token→BotToken"]
        J -- 其他 --> M[不做額外處理]
    end
```

### 頻道類型定義（靜態資料）

`Definitions` 是一個靜態的 `IReadOnlyList<ChannelTypeDefinition>`，定義每個頻道需要的設定欄位：

| 頻道 | 欄位 | 必填 | 機密 |
|------|------|------|------|
| **Line** | ChannelAccessToken | Yes | Yes |
| | ChannelSecret | No | Yes |
| | WebhookUrl | No | No |
| | WebhookMode | No | No |
| **Telegram** | BotToken | Yes | Yes |
| | WebhookUrl | No | No |
| | WebhookMode | No | No |
| **Email** | Host | Yes | No |
| | Port | Yes | No |
| | Username | Yes | No |
| | Password | Yes | Yes |

---

## EchoMessageProcessor

最簡單的 `IMessageProcessor` 實作：

```csharp
public Task<string> ProcessAsync(InboundMessage message, CancellationToken ct)
    => Task.FromResult($"[POC 回覆] 已收到：{message.Content}");
```

**擴展方式**：
1. 建立新類別實作 `IMessageProcessor`
2. 在 `DependencyInjection.cs` 替換註冊
3. 可整合 AI 對話引擎（OpenAI / Azure OpenAI）、規則引擎、或多策略路由

---

## ChannelFactory 與 ChannelSettingsResolver

雖然這兩個類別位於根目錄而非 `Services/`，但與業務服務緊密相關：

### ChannelFactory

- 建構時接收 `IEnumerable<IChannel>` → 建立 `Dictionary<string, IChannel>` 查找表
- `GetChannel(name)` → O(1) 查找，找不到拋出 `KeyNotFoundException`
- `GetDefinitions()` → 回傳所有已註冊頻道的 `ChannelDefinition` 清單

### ChannelSettingsResolver

靜態輔助類別，以 6 種策略從 `ChannelConfig` 中模糊匹配頻道設定：

| 優先順序 | 策略 | 範例 |
|---------|------|------|
| 1 | 字典直接查找 | key = `telegram` |
| 2 | 不區分大小寫完整比對 | key = `Telegram` |
| 3 | 前綴比對 | key = `telegram_prod` |
| 4 | 後綴比對 | key = `prod_telegram` |
| 5 | 包含比對 | key = `my-telegram-bot` |
| 6 | 特徵參數推斷 | Parameters 含 `BotToken` → 推斷為 Telegram |
