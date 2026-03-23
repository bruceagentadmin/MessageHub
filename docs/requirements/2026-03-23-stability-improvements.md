# MessageHub 穩定性與維護性改進計畫

> **建立日期**: 2026-03-23
> **狀態**: 草案 - 待討論確認
> **範圍**: 全系統審計（Core / Domain / Infrastructure / Api / Tests）

---

## 目錄

1. [審計摘要](#審計摘要)
2. [P0 — 關鍵問題（立即修正）](#p0--關鍵問題立即修正)
3. [P1 — 高優先度（短期改進）](#p1--高優先度短期改進)
4. [P2 — 中優先度（中期改進）](#p2--中優先度中期改進)
5. [P3 — 低優先度（長期優化）](#p3--低優先度長期優化)
6. [實施建議順序](#實施建議順序)

---

## 審計摘要

本次審計從以下五個面向全面掃描了 MessageHub 現有程式碼：

| 面向 | 主要發現數 | 關鍵風險 |
|------|-----------|---------|
| 錯誤處理 | 8 項 | ChannelManager catch 區塊未防護、無全域例外 middleware、空 catch 吞錯 |
| DI / 生命週期 | 7 項 | 硬編碼設定值、無 IOptions/appsettings、SqliteMessageLogRepository 雙介面耦合 |
| 併發 / 效能 | 10 項 | SQLite 無 WAL、ChannelManager 單線程瓶頸、Unbounded Channel、設定檔無快取 |
| 測試覆蓋 | 9 項 | Domain/Infrastructure 層無測試、無整合測試、測試/生產環境差異 |
| 日誌 / 可觀測性 | 10 項 | 大量元件無 ILogger、無 request logging、health endpoint 無依賴檢查、無 metrics |

---

## P0 — 關鍵問題（立即修正）

### P0-1: ChannelManager catch 區塊可能導致背景服務終止

**檔案**: `src/MessageHub.Core/Bus/ChannelManager.cs` (ProcessMessageAsync)
**問題**: catch 區塊內呼叫 `PublishDeadLetterAsync` 與 `logStore.AddAsync`，若這些呼叫本身拋出例外（如 DB 連線失敗、Channel 已關閉），例外會向上冒泡導致 BackgroundService 終止，整個訊息發送管線停擺。
**建議**:
- 在 catch 內對 DLQ/log 寫入再包一層 `try/catch`，失敗時使用 fallback（如寫入檔案或 emit metric）
- 區分 `OperationCanceledException`（host 停止）與一般例外，取消時不應將訊息移入 DLQ

### P0-2: 缺少全域例外處理 Middleware

**檔案**: `src/MessageHub.Api/Program.cs`
**問題**: 未註冊 `UseExceptionHandler` 或自訂例外處理 middleware，未捕獲的例外會由 ASP.NET Core 預設處理，導致不一致的錯誤回應格式與 log 格式。
**建議**:
- 加入 `app.UseExceptionHandler(...)` 或自訂 middleware
- 統一回傳結構化錯誤回應（含 correlation id）
- 開發環境啟用 `DeveloperExceptionPage`

### P0-3: SQLite 未啟用 WAL 模式與 Busy Timeout

**檔案**:
- `src/MessageHub.Infrastructure/Persistence/DatabaseInitializer.cs`
- `src/MessageHub.Infrastructure/Persistence/SqliteConnectionFactory.cs`

**問題**: 多個 Singleton repository 同時以短命 connection 寫入 SQLite，但未啟用 WAL（Write-Ahead Logging）也未設定 `busy_timeout`，高併發時會產生 `SQLITE_BUSY` / locked 錯誤。
**建議**:
- 在 `DatabaseInitializer.InitializeAsync` 中加入：
  ```sql
  PRAGMA journal_mode = WAL;
  PRAGMA synchronous = NORMAL;
  PRAGMA busy_timeout = 5000;
  ```
- 或在 `SqliteConnectionFactory.CreateConnection()` 後統一執行 PRAGMA

### P0-4: Health Endpoint 無依賴檢查

**檔案**: `src/MessageHub.Api/Program.cs` (lines 29-34)
**問題**: `/health` 只回傳靜態 `{ status: "ok" }`，未檢查 SQLite 連線、背景 worker 健康狀態等。無法作為 readiness probe。
**建議**:
- 擴展為 readiness endpoint，檢查 SQLite connectivity（`SELECT 1`）
- 檢查 ChannelManager 是否仍在運行

### P0-5: Webhook Controller 空 catch 吞掉錯誤

**檔案**:
- `src/MessageHub.Api/Controllers/TelegramWebhookController.cs` (lines 20-41)
- `src/MessageHub.Api/Controllers/LineWebhookController.cs` (lines 25-41)

**問題**: Webhook 解析失敗時的 `catch` 區塊無任何 logging，錯誤被完全吞掉，生產環境無法追蹤解析失敗原因。
**建議**:
- 至少加入 `logger.LogWarning(ex, "Webhook payload 解析失敗")`
- 對可預期的例外記錄 debug/trace 級別

---

## P1 — 高優先度（短期改進）

### P1-1: 硬編碼設定值 — 引入 IOptions / appsettings.json

**檔案與硬編碼位置**:
| 檔案 | 硬編碼內容 |
|------|-----------|
| `Infrastructure/DependencyInjection.cs` (lines 16-19) | DB 路徑 `"data/messagehub.db"` |
| `Core/Stores/JsonChannelSettingsStore.cs` (lines 28-37) | 設定檔路徑（往上 5 層推算） |
| `Core/Stores/JsonChannelSettingsStore.cs` (lines 170-181) | devtunnel webhook URL |
| `Core/Stores/InMemoryMessageLogStore.cs` (lines 29-35) | buffer 上限 500、回傳上限 200 |
| `Infrastructure/PollyRetryPipeline.cs` (lines 13-21) | 重試次數 3、延遲 1s |
| `Core/Bus/ChannelManager.cs` (line ~155) | DeadLetter retry count = 3 |

**建議**:
- 建立 `appsettings.json` + `appsettings.Development.json`
- 定義 Options 類別：`DatabaseOptions`、`RetryOptions`、`StoreOptions`
- 使用 `IOptions<T>` 注入，支援環境變數 override

### P1-2: ChannelManager 單線程吞吐瓶頸

**檔案**: `src/MessageHub.Core/Bus/ChannelManager.cs` (lines 61-76)
**問題**: 單一 BackgroundService 以 `await foreach` 逐條處理 Outbound 訊息，不同頻道也被串行化，導致整體吞吐受限。
**建議**:
- 改為啟動 N 個 worker Task 並行消費，保持 per-channel SemaphoreSlim 保證同頻道序列化
- 或在收到訊息後 non-blocking 啟動 background Task 處理，搭配全域 SemaphoreSlim 控制最大併發度
- 對 `_rateLimiters` 加入 TTL/清理機制，並在清除時 Dispose 對應 SemaphoreSlim

### P1-3: MessageBus Unbounded Channel 記憶體風險

**檔案**: `src/MessageHub.Core/Bus/MessageBus.cs` (lines 21-30)
**問題**: `_outbound` / `_inbound` / `_deadLetter` 皆為 `Channel.CreateUnbounded`，暴量時會無限佔用記憶體。
**建議**:
- 改用 `BoundedChannel` 或在 Publish 時加入 backpressure（限制排隊深度）
- 至少加入 `OutboundPendingCount` 的監控與告警閾值

### P1-4: 缺少 Request/Response Access Logging

**檔案**: `src/MessageHub.Api/Program.cs`
**問題**: 未註冊 `AddHttpLogging` / `UseSerilogRequestLogging` 等 middleware，缺乏 HTTP 層面的 access log（method/path/status/latency）。
**建議**:
- 加入 ASP.NET Core `AddHttpLogging` 或 Serilog `RequestLogging` middleware
- 注入 correlation id middleware 以串聯 request 追蹤

### P1-5: ChannelSettings 每次讀檔無快取

**檔案**:
- `src/MessageHub.Core/Services/ChannelSettingsService.cs` (lines 90-94)
- `src/MessageHub.Core/Stores/JsonChannelSettingsStore.cs` (lines 49-69)

**問題**: `GetAsync` 每次呼叫都做 `File.ReadAllTextAsync` + JSON 反序列化。ChannelManager 每處理一則訊息就呼叫一次，造成大量磁碟 I/O。
**建議**:
- 在 `ChannelSettingsService` 加入記憶體快取（TTL 30s 或 `FileSystemWatcher`）
- `SaveAsync` 完成後同步更新快取

### P1-6: HttpClient 未使用 IHttpClientFactory

**檔案**:
- `src/MessageHub.Core/Channels/TelegramChannel.cs` (line 18)
- `src/MessageHub.Core/Channels/LineChannel.cs` (line 19)
- `src/MessageHub.Api/Controllers/LineWebhookController.cs` (line 17)

**問題**: 直接 `new HttpClient()` 或在 constructor 接收，未使用 `IHttpClientFactory`，長時間運行可能遇到 DNS 快取或 socket 耗盡問題。
**建議**:
- 使用 `services.AddHttpClient<TelegramChannel>()` / `services.AddHttpClient<LineChannel>()` 等 typed client 模式
- 整合 Polly retry policy 到 HttpClient pipeline

### P1-7: JsonChannelSettingsStore 解析錯誤被靜默吞掉

**檔案**: `src/MessageHub.Core/Stores/JsonChannelSettingsStore.cs` (TryDeserializeCurrent/TryDeserializeLegacy)
**問題**: `JsonException` 被捕獲後直接回傳 `null`，無任何 log，設定檔錯誤完全不可見。
**建議**:
- 在 catch 中加入 `logger.LogWarning(ex, "設定檔解析失敗: {FilePath}", _filePath)`

---

## P2 — 中優先度（中期改進）

### P2-1: Domain / Infrastructure 層完全無測試

**缺少測試的模組**:
| 模組 | 類別 | 風險 |
|------|------|------|
| Domain | `HistoryService` | 查詢邏輯未驗證 |
| Domain | `ContactService` | CRUD 委派未驗證 |
| Domain | `MessagingService` | 僅有間接覆蓋（透過 Controller 測試） |
| Infrastructure | `SqliteMessageLogRepository` | SQL 查詢、分頁、LIKE 搜尋未驗證 |
| Infrastructure | `SqliteRecentTargetStore` | ON CONFLICT / COLLATE NOCASE 未驗證 |
| Infrastructure | `SqliteContactRepository` | Upsert / COALESCE 行為未驗證 |
| Infrastructure | `DatabaseInitializer` | Schema 建立未驗證 |
| Core | `ChannelManager` | 背景消費/重試/DLQ 流程未驗證 |
| Core | `PollyRetryPipeline` | 重試行為未驗證 |

**建議**:
- 新增 `SqlitePersistenceTests.cs`：使用暫時性 SQLite in-memory DB 測試所有 repository
- 新增 `HistoryServiceTests.cs`、`ContactServiceTests.cs`：mock repository 驗證委派邏輯
- 新增 `MessagingServiceTests.cs`：驗證 `HandleInboundAsync` 的 contact upsert 行為

### P2-2: 測試 / 生產環境差異（Parity Gap）

**問題**: 生產使用 SQLite（Dapper），測試使用自定義 in-memory fakes。SQL 查詢正確性、ON CONFLICT 行為、timestamp 格式等完全未被測試覆蓋。
**建議**:
- 使用 `WebApplicationFactory` 建立整合測試，搭配真實 SQLite（暫時 DB）
- 至少覆蓋：webhook 接收 → DB 寫入 → history 查詢 的端到端流程

### P2-3: 大量元件缺少 ILogger

**無 ILogger 的關鍵元件**:
- `MessageCoordinator`（Core 層核心協調器）
- `TelegramChannel` / `LineChannel`（外部 API 呼叫）
- 所有 SQLite Repository / Store（DB 操作）
- `ChannelSettingsService`（設定檔 I/O）

**建議**:
- 為上述元件注入 `ILogger<T>` 並在關鍵事件記錄結構化 log
- 統一使用 `{TenantId}` / `{MessageId}` / `{Channel}` 等範本參數

### P2-4: MessageBus 無 Graceful Drain 機制

**檔案**: `src/MessageHub.Core/Bus/MessageBus.cs`
**問題**: 關機時 `stoppingToken` 取消會直接中斷 `ReadAllAsync`，佇列內剩餘訊息會遺失。
**建議**:
- 提供 `CompleteWriter()` / `Drain(timeout)` API
- ChannelManager 在接收停止信號後嘗試處理完剩餘訊息（設定 configurable timeout）

### P2-5: SQLite 查詢效能 — LIKE '%keyword%' 全表掃描

**檔案**: `src/MessageHub.Infrastructure/Persistence/SqliteMessageLogRepository.cs` (lines 132-134)
**問題**: 內容搜尋使用 `LIKE '%keyword%'`，無 FTS 索引，大資料量下效能線性退化。COUNT + SELECT 造成掃描加倍。
**建議**:
- 評估引入 SQLite FTS5 全文索引
- 或限制內容搜尋的使用情境與頻率

### P2-6: SqliteMessageLogRepository 雙介面設計

**檔案**: `src/MessageHub.Infrastructure/Persistence/SqliteMessageLogRepository.cs`
**問題**: 同時實作 `IMessageLogStore`（Core）與 `IMessageLogRepository`（Domain），以同一 Singleton 實例註冊給兩個不同層的介面，模糊了模組邊界。
**建議**:
- 短期：文件化此設計決策
- 長期：拆分為 `SqliteMessageLogDao`（內部共用） + 兩個 Adapter 各自實作對應介面

### P2-7: DatabaseInitializer 缺少 CancellationToken

**檔案**:
- `src/MessageHub.Infrastructure/Persistence/DatabaseInitializer.cs` (line 11)
- `src/MessageHub.Infrastructure/DependencyInjection.cs` (line 38)

**問題**: `InitializeAsync` 無 `CancellationToken` 參數，啟動時無法隨 host 停止取消。
**建議**:
- 新增 `CancellationToken` 參數
- 在 `Program.cs` 傳入 `app.Lifetime.ApplicationStopping`

### P2-8: 測試品質問題

**問題**:
- `UnitTest1.cs` 為空的 placeholder 測試（無 assertion）
- 多個測試檔案重複定義 inline fakes（FakeMessageBus、StubHttpMessageHandler 等）

**建議**:
- 移除或替換 `UnitTest1.cs`
- 將共用 fakes 集中到 `tests/TestFixtures/` 或共用 helper 類別

---

## P3 — 低優先度（長期優化）

### P3-1: 引入 OpenTelemetry / Metrics

- 整合 OpenTelemetry（Traces + Metrics）
- 收集基本 metrics：`outbound_queue_length`、`processed_count`、`failed_count`、`dlq_count`
- MessageBus 已有 `PendingCount` 屬性可用來匯出

### P3-2: API 版本化與 Rate Limiting

- 目前無 API versioning（無 `/v1/...`）
- 無外部 rate limiting（webhook endpoints 暴露）
- 建議：視部署需求加入 API versioning 與 ASP.NET Core Rate Limiter

### P3-3: 認證與授權

- 目前 API 完全公開（`UseAuthorization` 已呼叫但無 auth scheme 註冊）
- 建議：至少加入 API Key 或 IP allowlist 保護非 webhook 端點

### P3-4: 使用者訊息內容持久化的隱私考量

**檔案**: `src/MessageHub.Infrastructure/Persistence/SqliteMessageLogRepository.cs`
**問題**: 使用者原始訊息（`Content`、`Details`）直接落地到 DB，未做 masking 或 redaction。ChannelManager 失敗時 `ex.Message` 也被寫入 `Details`（可能含敏感回應）。
**建議**:
- 引入 content redaction 策略或保留期限設定
- 避免將 `ex.Message` 完整寫入 DB（可能含 token 或 API 回應）

### P3-5: ChannelManager 職責過多

- 目前負責：消費、速率限制、設定載入驗證、重試、日誌、DLQ
- 建議：將設定解析/驗證抽成 `ChannelSettingsResolver`，日誌建構抽成獨立服務

### P3-6: EmailChannel 為 No-op

**檔案**: `src/MessageHub.Core/Channels/EmailChannel.cs`
**問題**: `SendAsync` 回傳 `Task.CompletedTask`，訊息會被標為完成但實際未寄送。
**建議**: 加入明確的 log 提示或在 `ChannelFactory` 標記為未實作

### P3-7: 前端 XSS 防護加強

**檔案**: `src/MessageHub.Api/wwwroot/index.html`
**問題**: 已有 `escapeHtml()` 保護大部分 innerHTML，但 `data-reply` 屬性使用 `encodeURIComponent` 而非 `escapeHtml`。
**建議**: 改用 `element.dataset` / `element.setAttribute()` 設定 data 屬性

---

## 實施建議順序

```
第一階段（1-2 天）— 關鍵修正
├── P0-1: ChannelManager catch 防護
├── P0-2: 全域例外 middleware
├── P0-3: SQLite WAL + busy_timeout
├── P0-4: Health endpoint 依賴檢查
└── P0-5: Webhook controller 空 catch 修正

第二階段（3-5 天）— 架構強化
├── P1-1: 引入 appsettings.json + IOptions
├── P1-2: ChannelManager 多 worker
├── P1-3: MessageBus bounded channel
├── P1-5: ChannelSettings 快取
├── P1-6: HttpClient → IHttpClientFactory
└── P1-7: JsonChannelSettingsStore 錯誤 log

第三階段（1-2 週）— 品質提升
├── P2-1: Domain/Infrastructure 測試補齊
├── P2-2: 整合測試（WebApplicationFactory + SQLite）
├── P2-3: ILogger 全面注入
├── P2-4: MessageBus graceful drain
├── P1-4: Request/response logging
└── P2-5 ~ P2-8: 其他中優先度項目

第四階段（持續改進）
├── P3-1: OpenTelemetry / Metrics
├── P3-2 ~ P3-7: 其他長期項目
└── 依實際上線需求調整優先度
```

---

> **下一步**: 請檢閱本文件，確認各項目的優先度與實施方向是否符合預期。確認後再開始實作。
