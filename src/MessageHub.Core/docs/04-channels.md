# 04 — Channels 頻道實作

> 本文件詳述 `Channels/` 資料夾下的所有頻道實作。

---

## 總覽

| 類別 | 實作介面 | 說明 |
|------|---------|------|
| `TelegramChannel` | `IChannel` | Telegram Bot API 訊息收發 |
| `LineChannel` | `IChannel` | LINE Messaging API 訊息收發 |
| `EmailChannel` | `IChannel` | Email 模擬通道（POC no-op）|

> **已遷移至 Domain 層**：`NotificationService`（`INotificationService`）與 `WebhookVerificationService`（`IWebhookVerificationService`）原先位於此資料夾，現已移至 `MessageHub.Domain.Services`。

---

## 頻道類別圖

```mermaid
classDiagram
    class IChannel {
        <<interface>>
        +Name : string
        +ParseRequestAsync()
        +SendAsync()
    }

    class TelegramChannel {
        -_channelSettingsService
        -_httpClient
        +Name = "telegram"
        -GetSettingsAsync()
    }

    class LineChannel {
        -_channelSettingsService
        -_httpClient
        +Name = "line"
        -GetSettingsAsync()
    }

    class EmailChannel {
        -_channelSettingsService
        +Name = "email"
    }

    IChannel <|.. TelegramChannel
    IChannel <|.. LineChannel
    IChannel <|.. EmailChannel

    TelegramChannel --> IChannelSettingsService : 讀取 BotToken
    LineChannel --> IChannelSettingsService : 讀取 ChannelAccessToken
    EmailChannel --> IChannelSettingsService : 保留供未來使用
```

---

## TelegramChannel

### ParseRequestAsync

將 `WebhookTextMessageRequest` 直接對映為 `InboundMessage`，無需 Telegram 特定的 Update 物件解析（通用 Webhook 端點已預先簡化）。

### SendAsync 流程

```mermaid
flowchart TD
    A[SendAsync 開始] --> B{settings 已傳入?}
    B -- 否 --> C[GetSettingsAsync<br/>從設定服務讀取]
    B -- 是 --> D[使用傳入的 settings]
    C --> D
    D --> E[取出 BotToken]
    E --> F{BotToken 有值?}
    F -- 否 --> G[拋出 InvalidOperationException]
    F -- 是 --> H[POST https://api.telegram.org<br/>/bot{token}/sendMessage]
    H --> I[EnsureSuccessStatusCode]
```

**API 端點**：`POST https://api.telegram.org/bot{BotToken}/sendMessage`

**Payload 格式**：
```json
{ "chat_id": "目標Chat ID", "text": "訊息內容" }
```

---

## LineChannel

### SendAsync 流程

```mermaid
flowchart TD
    A[SendAsync 開始] --> B{settings 已傳入?}
    B -- 否 --> C[GetSettingsAsync<br/>從設定服務讀取]
    B -- 是 --> D[使用傳入的 settings]
    C --> D
    D --> E[取出 ChannelAccessToken]
    E --> F{Token 有值?}
    F -- 否 --> G[拋出 InvalidOperationException]
    F -- 是 --> H[POST https://api.line.me<br/>/v2/bot/message/push]
    H --> I[設定 Authorization: Bearer token]
    I --> J[EnsureSuccessStatusCode]
```

**API 端點**：`POST https://api.line.me/v2/bot/message/push`

**Payload 格式**：
```json
{
  "to": "目標 User/Group ID",
  "messages": [{ "type": "text", "text": "訊息內容" }]
}
```

**認證方式**：`Authorization: Bearer {ChannelAccessToken}`

---

## EmailChannel

目前為 **POC no-op 實作**：

- `ParseRequestAsync`：正常解析為 `InboundMessage`
- `SendAsync`：直接返回 `Task.CompletedTask`，不執行實際 SMTP 發送

**未來擴展方向**：整合 MailKit 或 SmtpClient，從 `ChannelSettings.Parameters` 讀取 Host / Port / Username / Password。

---

## 設定讀取共通模式

三個頻道實作（Telegram / Line / Email）都遵循相同的設定讀取模式：

```csharp
// 優先使用外部傳入的 settings（ChannelManager 預載入）
settings ??= await GetSettingsAsync(cancellationToken);

// 從 Parameters 字典取出特定鍵值
var token = settings?.Parameters.GetValueOrDefault("BotToken")?.Trim();
```

**設計意圖**：
- `ChannelManager` 在處理訊息前會預先載入設定，避免每則訊息都讀取 JSON 檔案
- 直接呼叫 `SendAsync` 時（如測試），可不傳 settings，由頻道自行讀取
