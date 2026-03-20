# 聊天頻道系統規格書 (C# 多租戶版本)

> 本文件描述基於 .NET 8/9 的多租戶聊天頻道子系統設計。  
> 支援 Line, Telegram, Email, Slack, FTP, SFTP 等多種頻道，具備處理使用者訊息與系統主動通知之能力。

---

## 1. 系統架構圖 (System Overview)

```mermaid
graph TD
    subgraph platform ["外部平台 (Line, TG, Email, etc.)"]
        EP[平台設備/伺服器]
    end

    subgraph entry ["系統入口"]
        WC[WebhookController]
        CR[Cron/Scheduler]
    end

    subgraph logic ["多租戶處理核心"]
        PP[ICommonParameterProvider]
        CF[ChannelFactory]
        MP[MessageProcessor]
        NS[NotificationService]
    end

    EP <-->|Webhook / API| WC
    CR --> NS
    WC --> PP
    NS --> PP
    PP --> CF
    CF --> MP
    MP --> EP
    NS --> CF
```

---

## 2. 類別圖 (Class Diagram)

```mermaid
classDiagram
    class IChannel {
        <<interface>>
        +Name: string
        +ParseRequestAsync(request, tenantId) InboundMessage
        +SendAsync(chatId, message, settings) Task
    }

    class ICommonParameterProvider {
        <<interface>>
        +GetParameterByKeyAsync~T~(key) T
    }

    class IMessageProcessor {
        <<interface>>
        +ProcessAsync(message) string
    }

    class INotificationService {
        <<interface>>
        +SendNotificationAsync(tenantId, channel, msg) Task
    }

    class ChannelConfig {
        +TenantId: Guid
        +Channels: Map~string, ChannelSettings~
    }

    IChannel <|.. LineChannel
    IChannel <|.. TelegramChannel
    IMessageProcessor <|.. UnifiedProcessor

    ChannelFactory --> IChannel : 建立
    WebhookController --> ICommonParameterProvider
    WebhookController --> ChannelFactory
    WebhookController --> IMessageProcessor
    NotificationService --> ICommonParameterProvider
    NotificationService --> ChannelFactory
```

---

## 3. 循序圖 (Sequence Diagrams)

### 3.1 使用者訊息發送與系統回應 (Inbound)

```mermaid
sequenceDiagram
    participant User as 使用者
    participant Plat as 通訊平台
    participant WC as WebhookController
    participant PP as ICommonParameterProvider
    participant CF as ChannelFactory
    participant MP as MessageProcessor
    participant Ch as IChannel實例

    User->>Plat: 發送訊息
    Plat->>WC: Webhook POST (tenantId, channel)
    WC->>PP: GetParameterByKeyAsync<ChannelConfig>("ChannelConfig")
    PP-->>WC: ChannelConfig (JSON)
    WC->>CF: GetChannel(channel)
    CF-->>WC: IChannel 實例
    WC->>Ch: ParseRequestAsync(request)
    Ch-->>WC: InboundMessage
    WC->>MP: ProcessAsync(message)
    MP-->>WC: 回覆文字
    WC->>Ch: SendAsync(chatId, Response)
    Ch->>Plat: API Call (Send Message)
    Plat->>User: 顯示回覆
```

#### 3.1.1 處理流程圖 (Inbound Flowchart)

```mermaid
flowchart LR
    Start([通訊平台 Webhook 呼叫]) --> Receive[WebhookController 接收請求]
    Receive --> LoadConfig[透過 ParameterProvider 載入租戶頻道配置]
    LoadConfig --> GetChannel[ChannelFactory 建立對應 IChannel 實例]
    GetChannel --> Parse[IChannel 解析 Request 為 InboundMessage]
    Parse --> Process[IMessageProcessor 處理訊息並產生回覆文字]
    Process --> Send[IChannel 發送 SendAsync]
    Send --> Platform[呼叫外部平台 API]
    Platform --> End([使用者收到回覆])
```

### 3.2 系統主動定時發送通知 (Outbound/Notification)

```mermaid
sequenceDiagram
    participant Cron as 定時排程器
    participant NS as NotificationService
    participant PP as ICommonParameterProvider
    participant CF as ChannelFactory
    participant Ch as IChannel實例
    participant Plat as 通訊平台
    participant User as 使用者

    Cron->>NS: 觸發通知任務 (tenantId, msg)
    NS->>PP: GetParameterByKeyAsync<ChannelConfig>("ChannelConfig")
    PP-->>NS: ChannelConfig (JSON)
    NS->>CF: GetChannel(channel)
    CF-->>NS: IChannel 實例
    NS->>Ch: SendAsync(targetChatId, msg)
    Ch->>Plat: API Call (Send Notification)
    Plat->>User: 顯示通知訊息
```

#### 3.2.1 定時通知流程圖 (Notification Flowchart)

```mermaid
flowchart LR
    Start([定時排程觸發]) --> NS[NotificationService 啟動任務]
    NS --> LoadConfig[透過 ParameterProvider 載入租戶頻道配置]
    LoadConfig --> GetChannel[ChannelFactory 建立對應 IChannel 實例]
    GetChannel --> GetTarget[從配置中取得 NotificationTargetId]
    GetTarget --> Send[IChannel 發送 SendAsync]
    Send --> Platform[呼叫外部平台 API]
    Platform --> End([使用者收到通知訊息])
```

---

## 4. 訊息接收決策流程圖 (Flowchart)

```mermaid
flowchart TD
    Start([收到訊息/觸發通知]) --> CheckType{來源類型?}
    
    CheckType -- Webhook --> LoadTenant[依 URL 載入 ChannelConfig]
    CheckType -- Scheduler --> LoadTenant
    
    LoadTenant --> CheckEnable{該頻道已啟用?}
    CheckEnable -- 否 --> End([結束])
    CheckEnable -- 是 --> Parse[解析訊息/準備內容]
    
    Parse --> Dispatch{處理邏輯}
    Dispatch -- 關鍵字/AI --> Respond[產生回覆文字]
    Dispatch -- 定時通知 --> Respond
    
    Respond --> Send[呼叫平台 API 發送]
    Send --> End
```

---

## 5. 資料結構 (JSON 設定範例)

存於 `Tenants` 資料表的 `ConfigsJson` 欄位：

```json
{
  "channels": {
    "line": {
      "enabled": true,
      "parameters": {
        "channelToken": "...",
        "channelSecret": "...",
        "NotificationTargetId": "U12345678..."
      }
    },
    "telegram": {
      "enabled": true,
      "parameters": {
        "botToken": "...",
        "NotificationTargetId": "-1001234567"
      }
    }
  }
}
```

---

## 6. 類別清單 (Class List)

以下列出系統中主要的參與類別與介面：

### 6.1 基礎介面 (Core Interfaces)

| 類別/介面名稱 | 用途簡述 | 類別描述（功能與屬性） |
| :--- | :--- | :--- |
| `IChannel` | 頻道通用介面 | 定義所有通訊頻道的基礎行為。<br>• `Name`: 頻道識別名稱 (如 Line, Telegram)。<br>• `ParseRequestAsync`: 解析來自平台的 Webhook 請求並轉換為 `InboundMessage`。<br>• `SendAsync`: 發送 `OutboundMessage` 到指定的 `ChatId`。 |
| `ICommonParameterProvider` | 參數提供者 | 負責依鍵值 (Key) 異步獲取多租戶環境下的配置參數。<br>• `GetParameterByKeyAsync<T>`: 泛型方法，用於讀取特定的配置物件 (如 `ChannelConfig`)。 |
| `IMessageProcessor` | 訊息處理介面 | 定義對接收到的訊息進行商業邏輯處理的入口。<br>• `ProcessAsync`: 接收 `InboundMessage` 並回傳處理後的回覆文字。 |
| `INotificationService` | 通知服務介面 | 定義系統主動發送通知的標準行為。<br>• `SendGlobalNotificationAsync`: 根據租戶與頻道發送通知文字。 |

### 6.2 核心實作與邏輯 (Core Logic)

| 類別/介面名稱 | 用途簡述 | 類別描述（功能與屬性） |
| :--- | :--- | :--- |
| `ChannelFactory` | 頻道工廠 | 負責根據配置中的頻道名稱動態建立或取得對應的 `IChannel` 實例。<br>• `GetChannel`: 根據輸入字串回傳對應的頻道物件，若不支援則拋出異常。 |
| `UnifiedMessageProcessor` | 統一訊息處理核心 | 實現 `IMessageProcessor`，整合關鍵字、AI 分析或自動化腳本來決定回覆內容。<br>• `ProcessAsync`: 核心邏輯所在，封裝了處理進站訊息的所有判斷流程。 |
| `NotificationService` | 主動通知服務實作 | 實現 `INotificationService`。結合 `ChannelFactory` 與 `ICommonParameterProvider`。<br>• `SendGlobalNotificationAsync`: 從配置中尋找 `NotificationTargetId` 並發送訊息。 |
| `WebhookController` | Webhook 進入點 | 繼承自 `ControllerBase`，負責接收外部 HTTP POST 請求。<br>• `Handle`: 協調 `ParameterProvider` (取得配置)、`Channel` (解析訊息)、`Processor` (產生內容) 並完成發送。 |

### 6.3 資料模型 (Data Models)

| 類別/介面名稱 | 用途簡述 | 類別描述（功能與屬性） |
| :--- | :--- | :--- |
| `ChannelConfig` | 頻道總體配置 | 映射資料庫中 `Tenants` 表的 JSON 內容。<br>• `TenantId`: 租戶唯一識別碼。<br>• `Channels`: 儲存各頻道名稱與其對應 `ChannelSettings` 的字典。 |
| `ChannelSettings` | 頻道個別設定 | 存放單一頻道的開關與參數。<br>• `Enabled`: 是否啟用該頻道。<br>• `Parameters`: 儲存如 `Token`, `Secret`, `NotificationTargetId` 等字串參數。 |
| `InboundMessage` | 進站訊息封裝 | 標準化不同平台傳入的訊息資訊。<br>• `Content`: 訊息文字。<br>• `ChatId`: 對話識別碼。<br>• `OriginalPayload`: 原始原始請求物件。 |
| `OutboundMessage` | 出站訊息封裝 | 封裝準備發送至通訊平台的訊息。<br>• `Content`: 內容文字。<br>• `TargetId`: 發送對象 ID (可選)。 |

### 6.4 頻道具體實作 (Channel Providers)

| 類別/介面名稱 | 用途簡述 | 類別描述（功能與屬性） |
| :--- | :--- | :--- |
| `LineChannel` | Line 頻道實作 | 專門處理 Line Messaging API 的邏輯。<br>• 包含 Signature 驗證、LineEvent 解析與 API 呼叫。 |
| `TelegramChannel` | Telegram 頻道實作 | 專門處理 Telegram Bot API 的邏輯。<br>• 包含 Telegram Update 解析與 Bot Client 發送訊息。 |
