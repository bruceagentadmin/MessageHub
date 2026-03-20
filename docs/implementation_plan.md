# 多租戶 C# 頻道系統實作計畫

本計畫將設計一個具高度擴展性的多租戶架構，讓單一系統實體可以同時服務多個租戶並支援各種通訊頻道 (Line, Telegram, Email, Slack, FTP, SFTP)。

## 1. 核心設計架構

### 租戶辨識 (Tenant Identification)
- **Webhook 模式**: 使用具有 `{tenantId}` 的獨立端點，例如 `POST /api/webhooks/{channelName}/{tenantId}`。
- **租戶解析**: 注入 `TenantContext` 暫存目前請求的租戶資訊。

### 頻道管理 (Channel Management)
- **實作介面**: `IChannel` 介面提供統一的 `Receive` (處理來自通訊平台的訊息) 與 `Send` (發送訊息至通訊平台) 方法。
- **動態解析**: 使用 `ChannelFactory` 根據租戶設定中啟用的頻道與金鑰 (Token/Secret) 動態建立實例。

### 處理引擎 (Message Dispatcher)
- **統一介面**: 使用 `IMessageProcessor` 作為訊息處理核心，不再區分階段。
- **混合處理**: 內部可自訂邏輯，同時支援關鍵字、規則引擎或 AI。

### 主動通知機制 (Active Notification)
- **排程器**: 整合 `Cron` 或 `BackgroundService` 定時觸發。
- **分發器**: 根據租戶設定，將通知訊息派發至指定頻道。

## 2. 暫定資料結構 (Database Schema)

### `Tenants` 表 (預留)
- `Id`: Guid / string (PK)
- `Name`: string
- `Configs`: JSON (包含各頻道的 Token, Secret 等設定)

## 3. 預計實作元件

### [NEW] `IChannel` / `BaseChannel`
定義頻道的基礎合約。

### [NEW] `TenantContext` / `ITenantService`
處理目前請求的租戶快取與資訊提取。

### [NEW] `WebhookController` (多租戶版)
統一處理進站訊息與租戶分發的進入點。

## 4. 驗證計畫
- **單元測試**: 驗證不同 `{tenantId}` 的請求是否能正確解析到對應的設定。
- **手動驗證**: 使用模擬 Webhook 工具 (如 Postman) 發送 Line 與 Telegram 訊息，確保系統回覆硬編碼文字。
