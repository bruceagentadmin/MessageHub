# MessageHub

POC 驗證使用 .NET 8 建立多頻道統一通訊平台的可行性。

## 專案目標

1. 建立簡易的使用者介面，可觀察整體服務狀況
2. 初期支援 Line、Telegram、Email 三種頻道
3. 各頻道可發送訊息到中心，中心可回傳訊息到各頻道
4. 中心可廣播訊息到各頻道
5. 中心可接收各頻道的訊息並轉發到其他頻道
6. 設定檔可配置各頻道資訊，並透過設定檔新增或移除頻道

## 專案結構

```
MessageHub/
├── src/
│   ├── MessageHub.Core/           # 核心層 — 介面、模型、業務邏輯實作
│   │   ├── Models/                # 資料模型 (InboundMessage, OutboundMessage, ChannelConfig 等)
│   │   ├── Services/              # 業務服務 (ChannelSettingsService, UnifiedMessageProcessor)
│   │   ├── Stores/                # 儲存實作 (InMemoryMessageLogStore, JsonChannelSettingsStore, RecentTargetStore)
│   │   ├── Channels/              # 頻道實作 (LineChannel, TelegramChannel, EmailChannel, NotificationService, WebhookVerificationService)
│   │   ├── Bus/                   # 訊息匯流排 (MessageBus) + 背景分發引擎 (ChannelManager)
│   │   ├── I*.cs                  # 核心介面 (IChannel, IMessageBus, IRetryPipeline, IMessageProcessor 等)
│   │   ├── ChannelFactory.cs      # 頻道工廠
│   │   └── ChannelSettingsResolver.cs
│   ├── MessageHub.Infrastructure/ # 基礎設施層 — 僅提供 Polly 重試管線實作
│   │   ├── PollyRetryPipeline.cs  # IRetryPipeline 實作：Polly 3 次指數退避重試
│   │   └── DependencyInjection.cs # DI 註冊擴充方法
│   └── MessageHub.Api/            # API 層 — ASP.NET Core Web API
│       ├── Controllers/           # Webhook + 控制中心 API
│       ├── wwwroot/               # 靜態前端頁面
│       └── Program.cs             # 應用程式進入點
└── tests/
    └── MessageHub.Tests/          # 單元測試 (xUnit, 15 個測試案例)
```

## 快速開始

### 環境需求

- .NET 8 SDK

### 啟動服務

```bash
cd src/MessageHub.Api
dotnet run
```

服務預設啟動於 `http://localhost:5000`（或依 launchSettings.json 設定）。

### 執行測試

```bash
dotnet test
```

## API 端點一覽

| 方法 | 路徑 | 說明 |
|------|------|------|
| `GET` | `/health` | 健康檢查 |
| `GET` | `/swagger` | Swagger UI（開發環境） |
| `POST` | `/api/webhooks/{channel}/{tenantId}/text` | 通用 Webhook 文字訊息接收 |
| `POST` | `/api/line/webhook` | Line Webhook 回呼 |
| `POST` | `/api/telegram/webhook` | Telegram Webhook 回呼 |
| `POST` | `/api/control/send` | 手動發送訊息 |
| `GET` | `/api/control/logs` | 取得最近訊息日誌 |
| `GET` | `/api/control/channels` | 列出已註冊頻道 |
| `GET` | `/api/control/channel-settings` | 取得頻道設定 |
| `POST` | `/api/control/channel-settings` | 儲存頻道設定 |
| `POST` | `/api/control/channel-settings/verify-webhook` | 驗證 Webhook 連線 |

## Postman 測試指南

以下示範如何使用 Postman 測試完整收發流程。

### 前置準備

1. 啟動服務 (`dotnet run`)
2. 確認服務運作：`GET http://localhost:5000/health`

### 測試 1：健康檢查

```
GET http://localhost:5000/health
```

預期回應：
```json
{
  "status": "ok",
  "service": "MessageHub",
  "time": "2025-01-01T00:00:00Z"
}
```

### 測試 2：模擬 Webhook 訊息接收（推薦用此端點測試）

此端點不需要實際的 Line/Telegram Token，適合本地測試。

```
POST http://localhost:5000/api/webhooks/telegram/demo-tenant/text
Content-Type: application/json

{
  "chatId": "chat-123",
  "senderId": "user-456",
  "content": "Hello from Postman!"
}
```

預期回應 (200 OK)：
```json
{
  "id": "...",
  "timestamp": "...",
  "tenantId": "demo-tenant",
  "channel": "telegram",
  "direction": "Inbound",
  "status": "Delivered",
  "targetId": "chat-123",
  "content": "Hello from Postman!",
  "source": "telegram webhook",
  "details": "Sender=user-456"
}
```

### 測試 3：手動發送訊息

```
POST http://localhost:5000/api/control/send
Content-Type: application/json

{
  "tenantId": "demo-tenant",
  "channel": "telegram",
  "targetId": "",
  "content": "Reply from control center",
  "triggeredBy": "Postman"
}
```

> `targetId` 留空時，系統會自動使用該頻道最近的互動對象。若先執行測試 2，此處會自動使用 `chat-123`。

### 測試 4：查看訊息日誌

```
GET http://localhost:5000/api/control/logs?count=20
```

### 測試 5：查看已註冊頻道

```
GET http://localhost:5000/api/control/channels
```

### 測試 6：頻道設定管理

**讀取設定：**
```
GET http://localhost:5000/api/control/channel-settings
```

**儲存設定：**
```
POST http://localhost:5000/api/control/channel-settings
Content-Type: application/json

{
  "channels": {
    "telegram": {
      "enabled": true,
      "parameters": {
        "BotToken": "your-telegram-bot-token",
        "WebhookUrl": "https://your-domain/api/telegram/webhook"
      }
    },
    "line": {
      "enabled": true,
      "parameters": {
        "ChannelAccessToken": "your-line-channel-access-token",
        "WebhookUrl": "https://your-domain/api/line/webhook"
      }
    }
  }
}
```

### 測試 7：驗證 Webhook 連線

```
POST http://localhost:5000/api/control/channel-settings/verify-webhook
Content-Type: application/json

{
  "channelId": "telegram"
}
```

### 完整收發流程測試

1. **儲存設定** — 設定 Telegram/Line 頻道的 Token（測試 6）
2. **模擬收訊** — 透過通用 Webhook 端點發送訊息（測試 2）
3. **查看日誌** — 確認 Inbound 訊息已記錄（測試 4）
4. **手動發訊** — 透過控制中心發送回覆（測試 3）
5. **查看日誌** — 確認 Outbound 訊息已排入佇列（測試 4）

> 注意：手動發送（測試 3）會將訊息排入 MessageBus 佇列，由背景的 ChannelManager 負責實際發送。若未設定有效的 Token，實際發送會失敗並進入 Dead Letter Queue，但 API 層面的流程仍可驗證。

## 技術架構

詳見 [docs/ARCHITECTURE_OVERVIEW.md](docs/ARCHITECTURE_OVERVIEW.md)。
