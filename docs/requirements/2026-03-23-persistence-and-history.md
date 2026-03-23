# 需求設計：架構重構、訊息持久化與歷史查詢

> **建立日期**：2026-03-23  
> **狀態**：✅ 已實作完成（2026-03-23）  
> **目標**：重構專案分層架構，將訊息流持久化至 SQLite，記錄聯絡人，並新增歷史查詢功能

---

## 1. 需求摘要

| 項目 | 說明 |
|------|------|
| 架構重構 | 新增 `MessageHub.Domain` 作為業務編排層；API 不再直接呼叫 Core，一律透過 Domain Service |
| 訊息持久化 | `MessageLogEntry` 改存 SQLite，重啟不遺失 |
| 聯絡人記錄 | 記錄曾互動過的使用者（Channel + PlatformUserId 為唯一鍵），免重複向 Line/Telegram 查詢 |
| 歷史查詢 | 前端新增頁籤，支援多條件篩選＋分頁 |
| 技術選型 | SQLite + Dapper，後續可換其他資料庫 |

---

## 2. 架構變動

### 2.1 現有架構

```
MessageHub.Api → MessageHub.Core → MessageHub.Infrastructure
                  (介面 + 業務邏輯 + InMemory Store)    (Polly 重試)
```

API 直接注入 Core 的 `IMessageCoordinator`、`IMessageLogStore` 等介面。

### 2.2 目標架構

```
MessageHub.Api
  └─→ MessageHub.Domain        (業務編排層：Domain Service 為唯一對外窗口)
        ├─→ MessageHub.Core    (通訊核心：頻道、訊息匯流排、Coordinator)
        └─→ Repository 介面    (定義於 Domain，實作於 Infrastructure)

MessageHub.Infrastructure
  ├─→ MessageHub.Domain        (實作 Repository 介面)
  └─→ MessageHub.Core          (實作 IMessageLogStore、IRecentTargetStore)
```

### 2.3 專案相依關係

```
API  ──→  Domain  ──→  Core
API  ──→  Infrastructure（僅用於 DI 註冊）
Infrastructure  ──→  Domain
Infrastructure  ──→  Core
```

> **Domain 不參考 Infrastructure**，遵循依賴反轉原則。

### 2.4 核心原則

| 原則 | 說明 |
|------|------|
| API 只碰 Domain Service | Controller 注入 Domain 的 Service 介面，不再直接注入 Core 的 `IMessageCoordinator` 等 |
| Domain Service 為編排者 | 負責呼叫 Core（通訊邏輯）+ Repository（持久化），組合出完整業務流程 |
| Repository 介面在 Domain | `IMessageLogRepository`、`IContactRepository` 定義在 Domain 層 |
| Repository 實作在 Infrastructure | SQLite + Dapper 實作放 Infrastructure/Persistence/ |
| Core 保持通訊職責 | Core 的 `IMessageCoordinator`、`IMessageBus`、Channel 等不變動職責 |

---

## 3. 新增專案：MessageHub.Domain

### 3.1 專案結構

```
MessageHub.Domain/
├── Models/
│   ├── Contact.cs                   ← 聯絡人實體
│   ├── MessageLogQuery.cs           ← 歷史查詢參數
│   ├── MessageLogRecord.cs          ← Domain 層日誌 Model（對等 Core 的 MessageLogEntry）
│   └── PagedResult.cs               ← 分頁結果泛型封裝
├── Repositories/
│   ├── IMessageLogRepository.cs     ← 訊息日誌 Repository 介面
│   └── IContactRepository.cs        ← 聯絡人 Repository 介面
├── Services/
│   ├── IMessagingService.cs         ← 訊息收發 Service 介面（包裝 Core 的 IMessageCoordinator）
│   ├── MessagingService.cs          ← 編排 Core Coordinator + Contact 更新
│   ├── IHistoryService.cs           ← 歷史查詢 Service 介面
│   ├── HistoryService.cs            ← 查詢 Repository
│   ├── IContactService.cs           ← 聯絡人 Service 介面
│   └── ContactService.cs            ← 聯絡人 CRUD
├── MessageHub.Domain.csproj
└── DependencyInjection.cs
```

### 3.2 專案參考

- **NuGet**：`Microsoft.Extensions.DependencyInjection.Abstractions`
- **ProjectReference**：`MessageHub.Core`（Domain 需呼叫 Core 的 `IMessageCoordinator`）

### 3.3 Models 說明

#### Contact

| 欄位 | 型別 | 說明 |
|------|------|------|
| Id | Guid | 系統內部唯一 ID |
| Channel | string | 頻道名稱（telegram / line / email） |
| PlatformUserId | string | 平台端使用者 ID（對應 InboundMessage.SenderId） |
| DisplayName | string? | 顯示名稱 |
| ChatId | string? | 最後互動的聊天室 ID（供後續主動發訊用） |
| FirstSeenAt | DateTimeOffset | 首次互動時間 |
| LastSeenAt | DateTimeOffset | 最後互動時間 |
| MessageCount | int | 累計訊息數 |

> 唯一鍵：`(Channel, PlatformUserId)`

#### MessageLogRecord

與 Core 的 `MessageLogEntry` 欄位對等，但為獨立型別以避免跨層相依。

| 欄位 | 說明 |
|------|------|
| Id, Timestamp, TenantId, Channel | 基本識別 |
| Direction (int), Status (int) | 使用 int 而非 Core 的 enum，避免 Domain 依賴 Core 的 enum 定義 |
| TargetId, TargetDisplayName, Content, Source, Details | 訊息內容與來源 |

> API 層負責 string ↔ int 的映射（`"Inbound"` → `0`，`"Outbound"` → `1` 等）。

#### MessageLogQuery

支援多條件篩選＋分頁的查詢參數物件：

- 篩選條件：Channel、Direction、Status、TargetId、SenderId、ContentKeyword、From、To
- 分頁參數：Page（預設 1）、PageSize（預設 50）

#### PagedResult\<T>

泛型分頁結果，包含 Items、TotalCount、Page、PageSize，計算 TotalPages / HasPrevious / HasNext。

### 3.4 Repository 介面

#### IMessageLogRepository

| 方法 | 說明 |
|------|------|
| `AddAsync(MessageLogRecord)` | 新增一筆日誌 |
| `GetRecentAsync(int count)` | 取得最近 N 筆（供即時 Log 面板用） |
| `QueryAsync(MessageLogQuery)` | 進階查詢（帶篩選＋分頁） |

#### IContactRepository

| 方法 | 說明 |
|------|------|
| `UpsertAsync(Contact)` | 新增或更新（以 Channel + PlatformUserId 做 UPSERT） |
| `GetByChannelAsync(string channel)` | 依頻道取得聯絡人清單 |
| `GetAllAsync()` | 取得所有聯絡人 |
| `FindAsync(string channel, string platformUserId)` | 查詢單一聯絡人 |

### 3.5 Domain Services

**原則**：API 層的所有 Controller 不可直接呼叫 Core 介面，必須透過 Domain Service。MessagingService 包裝 Core 的 **所有** 功能。

#### MessagingService（核心編排者）

**職責**：完整包裝 Core 層所有對外功能，API 層透過此 Service 操作一切通訊與設定功能。

注入：`IMessageCoordinator`（Core）、`IChannelSettingsService`（Core）、`IWebhookVerificationService`（Core）、`IContactRepository`（Domain）

| 方法 | 包裝對象 | 額外邏輯 |
|------|---------|---------|
| `HandleInboundAsync()` | `IMessageCoordinator.HandleInboundAsync()` | 呼叫後更新聯絡人（`IContactRepository.UpsertAsync`） |
| `SendManualAsync()` | `IMessageCoordinator.SendManualAsync()` | — |
| `GetRecentLogsAsync()` | `IMessageCoordinator.GetRecentLogsAsync()` | — |
| `GetChannels()` | `IMessageCoordinator.GetChannels()` | — |
| `GetChannelSettingsAsync()` | `IChannelSettingsService.GetAsync()` | — |
| `SaveChannelSettingsAsync()` | `IChannelSettingsService.SaveAsync()` | — |
| `GetChannelTypes()` | `IChannelSettingsService.GetChannelTypes()` | — |
| `GetSettingsFilePath()` | `IChannelSettingsService.GetSettingsFilePath()` | — |
| `VerifyWebhookAsync()` | `IWebhookVerificationService.VerifyAsync()` | — |

> **這是 API 層呼叫通訊與設定功能的唯一入口**，所有 Controller（ControlCenter、Webhooks、Line、Telegram）均改為注入 `IMessagingService`。

#### HistoryService

- 注入：`IMessageLogRepository`
- `QueryLogsAsync(MessageLogQuery)` → 委派給 Repository

#### ContactService

- 注入：`IContactRepository`
- `GetAllContactsAsync()` / `GetContactsByChannelAsync()` / `FindContactAsync()` → 委派給 Repository

### 3.6 DI 註冊

提供 `AddMessageHubDomain()` 擴充方法，註冊所有 Domain Service（MessagingService、HistoryService、ContactService）。

---

## 4. 修改：MessageHub.Core

### 4.1 DependencyInjection.cs

**移除**以下 InMemory Store 的 DI 註冊（改由 Infrastructure 的 SQLite 實作取代）：

- `IMessageLogStore` → `InMemoryMessageLogStore`（移除註冊）
- `IRecentTargetStore` → `RecentTargetStore`（移除註冊）

> InMemory 實作檔案保留不刪（測試可能需要），僅移除 DI 註冊。

### 4.2 MessageCoordinator

**不變動**。Core 的 MessageCoordinator 保持原有職責（處理通訊邏輯、寫入 IMessageLogStore、更新 IRecentTargetStore）。

聯絡人更新邏輯由 **Domain 的 MessagingService 負責**，不汙染 Core 層。

### 4.3 新增 Core → Domain 相依？

**不需要**。Core 不參考 Domain。Domain 參考 Core（呼叫 `IMessageCoordinator`），而非反過來。

---

## 5. 修改：MessageHub.Infrastructure

### 5.1 新增套件

| 套件 | 用途 |
|------|------|
| `Dapper` | 輕量 ORM |
| `Microsoft.Data.Sqlite` | SQLite 連線 |

### 5.2 新增專案參考

- `MessageHub.Domain`（實作 Repository 介面）
- `MessageHub.Core`（實作 `IMessageLogStore`、`IRecentTargetStore`）— 已存在

### 5.3 新增目錄結構

```
MessageHub.Infrastructure/
├── Persistence/
│   ├── DatabaseInitializer.cs          ← 啟動時 CREATE TABLE IF NOT EXISTS
│   ├── SqliteConnectionFactory.cs      ← 封裝 connectionString，建立連線
│   ├── SqliteMessageLogRepository.cs   ← 同時實作 IMessageLogStore (Core) + IMessageLogRepository (Domain)
│   ├── SqliteRecentTargetStore.cs      ← 實作 IRecentTargetStore (Core)
│   └── SqliteContactRepository.cs      ← 實作 IContactRepository (Domain)
├── PollyRetryPipeline.cs               ← (既有)
└── DependencyInjection.cs              ← (修改)
```

### 5.4 關鍵設計：SqliteMessageLogRepository

此類別 **同時實作兩個介面**，共用同一張 `message_logs` 表：

- `IMessageLogStore`（Core）：供 MessageCoordinator、ChannelManager 寫入/讀取日誌
- `IMessageLogRepository`（Domain）：供 HistoryService 進階查詢、供 MessagingService 取最近日誌

DI 註冊時以單一實例同時綁定兩個介面。

### 5.5 DI 註冊變動

`AddMessageHubInfrastructure()` 新增：

1. **SqliteConnectionFactory**：連線字串 `Data Source=data/messagehub.db`
2. **SqliteMessageLogRepository** → 同時註冊為 `IMessageLogStore` + `IMessageLogRepository`
3. **SqliteRecentTargetStore** → 註冊為 `IRecentTargetStore`
4. **SqliteContactRepository** → 註冊為 `IContactRepository`

新增 `InitializeDatabaseAsync()` 擴充方法：在 `app.Build()` 後、`app.Run()` 前呼叫，自動建表。

---

## 6. 修改：MessageHub.Api

### 6.1 Controller 層變動

**原則**：所有 Controller 不再持有任何 Core 層介面引用，一律改為注入 Domain 的 `IMessagingService`。

#### ControlCenterController

| 原本注入 | 改為 |
|---------|------|
| `IMessageCoordinator` | `IMessagingService` |
| `IChannelSettingsService` | `IMessagingService`（已包裝） |
| `IWebhookVerificationService` | `IMessagingService`（已包裝） |

所有端點改為呼叫 `IMessagingService` 對應方法，行為不變。

#### WebhooksController

| 原本注入 | 改為 |
|---------|------|
| `IMessageCoordinator` | `IMessagingService` |

`HandleInboundAsync` 改為呼叫 `IMessagingService.HandleInboundAsync()`。

#### LineWebhookController

| 原本注入 | 改為 |
|---------|------|
| `IMessageCoordinator` | `IMessagingService` |
| `IChannelSettingsService` | `IMessagingService`（已包裝） |

`HandleInboundAsync` 與 `GetAsync`（取 Line Token）改為呼叫 `IMessagingService` 對應方法。

#### TelegramWebhookController

| 原本注入 | 改為 |
|---------|------|
| `IMessageCoordinator` | `IMessagingService` |

`HandleInboundAsync` 改為呼叫 `IMessagingService.HandleInboundAsync()`。

#### 新增 HistoryController

| 端點 | 說明 |
|------|------|
| `GET /api/history/logs` | 進階查詢歷史訊息（多條件篩選＋分頁） |
| `GET /api/history/contacts` | 取得聯絡人清單（可選依頻道篩選） |

**查詢參數**（`/api/history/logs`）：

| 參數 | 型別 | 說明 |
|------|------|------|
| channel | string? | 頻道篩選 |
| direction | string? | 方向（inbound/outbound/system） |
| status | string? | 狀態（pending/delivered/failed） |
| targetId | string? | 目標 ID |
| senderId | string? | 發送者 ID |
| keyword | string? | 內容關鍵字（LIKE） |
| from | DateTimeOffset? | 起始時間 |
| to | DateTimeOffset? | 結束時間 |
| page | int | 頁碼（預設 1） |
| pageSize | int | 每頁筆數（預設 50，上限 200） |

### 6.2 Program.cs 變動

新增：
- `builder.Services.AddMessageHubDomain()`
- `await app.Services.InitializeDatabaseAsync()`

### 6.3 csproj 變動

新增 `MessageHub.Domain` 專案參考。

### 6.4 前端 index.html — 歷史紀錄頁籤

**UI 元素**：

1. **頁籤列**：「控制中心」/「歷史紀錄」切換（放在 hero 下方）
2. **歷史紀錄面板**：
   - 篩選區：頻道下拉、方向（全部/收/發）、狀態（全部/Pending/Delivered/Failed）、時間範圍（起/迄）、關鍵字、目標 ID
   - 結果表格：時間、頻道、方向、狀態、目標、內容、來源
   - 分頁控制：上一頁 / 下一頁 / 頁碼 / 總筆數

**API 對接**：呼叫 `GET /api/history/logs?{params}` 取得分頁資料並渲染。

---

## 7. 資料庫 Schema

三張表，啟動時自動建立：

### message_logs

| 欄位 | 型別 | 說明 |
|------|------|------|
| Id | TEXT PK | GUID |
| Timestamp | TEXT | ISO 8601 時間戳 |
| TenantId | TEXT | 租戶 ID |
| Channel | TEXT | 頻道名稱 |
| Direction | INTEGER | 0=Inbound, 1=Outbound, 2=System |
| Status | INTEGER | 0=Pending, 1=Delivered, 2=Failed |
| TargetId | TEXT | 目標 ID |
| TargetDisplayName | TEXT? | 目標顯示名稱 |
| Content | TEXT | 訊息內容 |
| Source | TEXT | 來源描述 |
| Details | TEXT? | 額外細節 |
| CreatedAt | TEXT | 建立時間（預設 datetime('now')） |

**索引**：Timestamp DESC、Channel、TargetId、Direction、Status

### recent_targets

| 欄位 | 型別 | 說明 |
|------|------|------|
| Channel | TEXT PK | 頻道名稱 |
| TargetId | TEXT | 最後互動目標 ID |
| DisplayName | TEXT? | 顯示名稱 |
| UpdatedAt | TEXT | 更新時間 |

### contacts

| 欄位 | 型別 | 說明 |
|------|------|------|
| Id | TEXT PK | GUID |
| Channel | TEXT | 頻道名稱 |
| PlatformUserId | TEXT | 平台使用者 ID |
| DisplayName | TEXT? | 顯示名稱 |
| ChatId | TEXT? | 聊天室 ID |
| FirstSeenAt | TEXT | 首次互動時間 |
| LastSeenAt | TEXT | 最後互動時間 |
| MessageCount | INTEGER | 累計訊息數（預設 0） |

**約束**：`UNIQUE(Channel, PlatformUserId)`  
**索引**：Channel、LastSeenAt DESC

---

## 8. NuGet 套件異動

| 專案 | 套件 | 版本 |
|------|------|------|
| MessageHub.Infrastructure | `Dapper` | 最新穩定版 |
| MessageHub.Infrastructure | `Microsoft.Data.Sqlite` | 匹配 .NET 8 |
| MessageHub.Domain | `Microsoft.Extensions.DependencyInjection.Abstractions` | 匹配 .NET 8 |

---

## 9. 檔案異動清單

### 新增檔案

| 檔案 | 說明 |
|------|------|
| `src/MessageHub.Domain/MessageHub.Domain.csproj` | Domain 專案檔 |
| `src/MessageHub.Domain/DependencyInjection.cs` | DI 註冊 |
| `src/MessageHub.Domain/Models/Contact.cs` | 聯絡人實體 |
| `src/MessageHub.Domain/Models/MessageLogQuery.cs` | 查詢參數 |
| `src/MessageHub.Domain/Models/MessageLogRecord.cs` | Domain 層日誌 Model |
| `src/MessageHub.Domain/Models/PagedResult.cs` | 分頁結果 |
| `src/MessageHub.Domain/Repositories/IMessageLogRepository.cs` | 日誌 Repository 介面 |
| `src/MessageHub.Domain/Repositories/IContactRepository.cs` | 聯絡人 Repository 介面 |
| `src/MessageHub.Domain/Services/IMessagingService.cs` | 訊息收發 Service 介面 |
| `src/MessageHub.Domain/Services/MessagingService.cs` | 訊息收發 Service 實作（編排者） |
| `src/MessageHub.Domain/Services/IHistoryService.cs` | 歷史查詢 Service 介面 |
| `src/MessageHub.Domain/Services/HistoryService.cs` | 歷史查詢 Service 實作 |
| `src/MessageHub.Domain/Services/IContactService.cs` | 聯絡人 Service 介面 |
| `src/MessageHub.Domain/Services/ContactService.cs` | 聯絡人 Service 實作 |
| `src/MessageHub.Infrastructure/Persistence/DatabaseInitializer.cs` | 建表初始化 |
| `src/MessageHub.Infrastructure/Persistence/SqliteConnectionFactory.cs` | 連線工廠 |
| `src/MessageHub.Infrastructure/Persistence/SqliteMessageLogRepository.cs` | 日誌 SQLite 實作（雙介面） |
| `src/MessageHub.Infrastructure/Persistence/SqliteRecentTargetStore.cs` | 最近目標 SQLite 實作 |
| `src/MessageHub.Infrastructure/Persistence/SqliteContactRepository.cs` | 聯絡人 SQLite 實作 |
| `src/MessageHub.Api/Controllers/HistoryController.cs` | 歷史查詢 API |

### 修改檔案

| 檔案 | 變動 |
|------|------|
| `src/MessageHub.Core/DependencyInjection.cs` | 移除 InMemoryMessageLogStore、RecentTargetStore 的 DI 註冊 |
| `src/MessageHub.Infrastructure/MessageHub.Infrastructure.csproj` | 新增 Dapper、Microsoft.Data.Sqlite、Domain 專案參考 |
| `src/MessageHub.Infrastructure/DependencyInjection.cs` | 新增 SQLite 連線工廠 + Repository 註冊 + DB 初始化方法 |
| `src/MessageHub.Api/MessageHub.Api.csproj` | 新增 Domain 專案參考 |
| `src/MessageHub.Api/Program.cs` | 新增 `AddMessageHubDomain()` + `InitializeDatabaseAsync()` |
| `src/MessageHub.Api/Controllers/ControlCenterController.cs` | 改注入 `IMessagingService`，取代 `IMessageCoordinator` + `IChannelSettingsService` + `IWebhookVerificationService` |
| `src/MessageHub.Api/Controllers/WebhooksController.cs` | 改注入 `IMessagingService`，取代 `IMessageCoordinator` |
| `src/MessageHub.Api/Controllers/LineWebhookController.cs` | 改注入 `IMessagingService`，取代 `IMessageCoordinator` + `IChannelSettingsService` |
| `src/MessageHub.Api/Controllers/TelegramWebhookController.cs` | 改注入 `IMessagingService`，取代 `IMessageCoordinator` |
| `src/MessageHub.Api/wwwroot/index.html` | 新增歷史紀錄頁籤 |

### 保留不動（不再 DI 註冊）

| 檔案 | 說明 |
|------|------|
| `src/MessageHub.Core/Stores/InMemoryMessageLogStore.cs` | 保留供測試用 |
| `src/MessageHub.Core/Stores/RecentTargetStore.cs` | 保留供測試用 |

---

## 10. 資料流程圖

### 收訊（Inbound）

```
Webhook Request
  → API Controller
    → Domain.MessagingService.HandleInboundAsync()
      ├─→ Core.IMessageCoordinator.HandleInboundAsync()
      │     ├─→ IMessageLogStore.AddAsync()        ← SQLite
      │     └─→ IRecentTargetStore.SetLastTargetAsync() ← SQLite
      └─→ IContactRepository.UpsertAsync()          ← SQLite
```

### 發訊（Outbound）

```
Control Center / API
  → API Controller
    → Domain.MessagingService.SendAsync()
      └─→ Core.IMessageCoordinator.SendAsync()
            ├─→ IMessageBus.PublishAsync()
            └─→ IMessageLogStore.AddAsync()          ← SQLite
```

### 歷史查詢

```
前端歷史頁籤
  → GET /api/history/logs?params
    → HistoryController
      → Domain.HistoryService.QueryLogsAsync()
        → IMessageLogRepository.QueryAsync()          ← SQLite
```

### 聯絡人查詢

```
前端歷史頁籤
  → GET /api/history/contacts?channel=xxx
    → HistoryController
      → Domain.ContactService.GetContactsByChannelAsync()
        → IContactRepository.GetByChannelAsync()      ← SQLite
```

---

## 11. 風險與注意事項

| 風險 | 說明 | 對策 |
|------|------|------|
| SQLite 檔案權限 | `data/messagehub.db` 需寫入權限 | 啟動時自動建立 `data/` 目錄 |
| 並行寫入 | SQLite WAL 模式支援一寫多讀 | POC 負載下足夠，正式環境換 DB |
| 換 DB 相容性 | Dapper 直接寫 SQL | `ON CONFLICT` 等語法需調整 |
| Model 映射 | Core `MessageLogEntry` ↔ Domain `MessageLogRecord` 欄位對等但獨立 | Infrastructure 層負責轉換 |
| 既有測試 | InMemory Store 不再註冊 | 測試專案需自行註冊 InMemory 實作 |
| ControlController 改動 | 所有 Controller 注入來源從 Core → Domain | 確保所有現有 API 行為不變 |

---

## 12. 確認事項紀錄

| 問題 | 決定 |
|------|------|
| MessagingService 是否包裝 Core 的所有功能？ | **是**，包含 IMessageCoordinator + IChannelSettingsService + IWebhookVerificationService 的全部方法 |
| ControlController 是否完全改用 Domain Service？ | **是**，所有 Controller 不可直接呼叫 Core，一律透過 Domain Service |
| Solution 檔 (.sln) 是否需要更新？ | 是，新增 MessageHub.Domain 專案需加入 solution |
