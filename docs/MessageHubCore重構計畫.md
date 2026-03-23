# MessageHub.Core 重構計畫

> 目標：提升 DX（Developer Experience），修正 SOLID 違反項，並為所有類別、方法、複雜邏輯補上完整的 XML 文件註解與行內註解。  
> **本重構已於 2026-03-24 完成實施。** 詳見 `refactor/simplify-core` 分支。

---

## 一、SOLID 違反分析與重構方案

### 1. 🔴 SRP 違反 — `UnifiedMessageProcessor` 承擔過多職責

**現況**：`UnifiedMessageProcessor` 同時負責：
- `IMessageProcessor.ProcessAsync`（產生 POC 回覆文字）
- `HandleInboundAsync`（Webhook 進站處理：解析 → 記錄 → 自動回覆 → 推送至 Bus）
- `SendManualAsync`（手動發送：解析 targetId → 推送至 Bus → 記錄日誌）
- `GetRecentLogsAsync` / `GetChannels`（查詢用的便利方法，委派給 Store/Factory）

**問題**：
- 一個類別混合了「訊息處理邏輯」、「訊息協調/調度邏輯」、「查詢代理」三種職責
- API Controller 直接依賴具體類別 `UnifiedMessageProcessor` 而非介面（DI 中註冊了具體型別）
- `HandleInboundAsync` 與 `SendManualAsync` 不屬於 `IMessageProcessor` 介面定義，呼叫端必須知道具體型別

**重構方案**：
1. 新增 `IMessageCoordinator` 介面，包含 `HandleInboundAsync`、`SendManualAsync`、`GetRecentLogsAsync`、`GetChannels`
2. 將 `UnifiedMessageProcessor` 拆分為：
   - `MessageProcessor : IMessageProcessor` — 純粹的訊息處理邏輯（ProcessAsync）
   - `MessageCoordinator : IMessageCoordinator` — 協調邏輯（進站處理、手動發送、查詢）
3. DI 註冊更新，API Controller 改為依賴 `IMessageCoordinator`

**影響範圍**：`Services/UnifiedMessageProcessor.cs`、`DependencyInjection.cs`、API Controllers

---

### 2. 🔴 ISP 違反 — `IChannelSettingsService` 繼承 `ICommonParameterProvider`

**現況**：
```csharp
public interface IChannelSettingsService : ICommonParameterProvider
{
    Task<ChannelConfig> GetAsync(...);
    Task<ChannelConfig> SaveAsync(...);
    IReadOnlyList<ChannelTypeDefinition> GetChannelTypes();
    string GetSettingsFilePath();
}
```

**問題**：
- `ICommonParameterProvider` 是一個泛型參數查詢介面，語意上與「頻道設定服務」不同層次
- 僅 `ChannelSettingsService` 同時實作兩者，強制綁定造成下游消費者被迫看到不需要的方法
- `GetChannelTypes()` 回傳 UI 用的欄位定義，與設定讀寫職責混雜
- `GetSettingsFilePath()` 是儲存層的實作細節洩漏到服務介面

**重構方案**：
1. 解除 `IChannelSettingsService : ICommonParameterProvider` 的繼承關係
2. `ChannelSettingsService` 分別獨立實作 `IChannelSettingsService` 與 `ICommonParameterProvider`（保持同一類別，透過 DI 分別註冊——這部分目前 DI 已如此做，只需解除介面繼承）
3. 將 `GetChannelTypes()` 移至獨立的 `IChannelTypeRegistry` 介面（或保留在 `IChannelSettingsService` 但以文件清楚標註其 UI 用途）
4. 將 `GetSettingsFilePath()` 從 `IChannelSettingsService` 移除，僅保留在 `IChannelSettingsStore` 介面（已存在）

**影響範圍**：`IChannelSettingsService.cs`、`ICommonParameterProvider.cs`、`Services/ChannelSettingsService.cs`、`DependencyInjection.cs`

---
 

## 二、XML 文件註解補齊範圍

以下為需要補齊或強化 XML 文件註解的檔案清單（✅ 表示現有註解已足夠，⚠️ 需補強，❌ 完全缺失）：

### 介面（根目錄 I*.cs）
| 檔案 | 現況 | 需補項目 |
|---|---|---|
| `IChannel.cs` | ✅ 已有 summary + method | 參數 `<param>` 與 `<returns>` 標籤 |
| `IMessageBus.cs` | ✅ 已有 summary | 各方法補 `<param>` 與 `<returns>` |
| `IMessageProcessor.cs` | ✅ 已有 summary | 補 `<param>` 與 `<returns>` |
| `INotificationService.cs` | ✅ 已有 summary | 補 `<param>`、`<exception>` 標籤 |
| `ICommonParameterProvider.cs` | ✅ 已有 summary | 補 `<typeparam>`、`<param>`、`<returns>` |
| `IChannelSettingsService.cs` | ⚠️ 只有類別 summary | 每個方法補 `<summary>`、`<param>`、`<returns>` |
| `IChannelSettingsStore.cs` | ⚠️ 只有類別 summary | 每個方法補 `<summary>`、`<param>`、`<returns>` |
| `IMessageLogStore.cs` | ⚠️ 只有類別 summary | 每個方法補 `<summary>`、`<param>`、`<returns>` |
| `IRecentTargetStore.cs` | ⚠️ 只有類別 summary | 每個方法補 `<summary>`、`<param>`、`<returns>` |
| `IRetryPipeline.cs` | ✅ 已有 summary + method | 補 `<param>` 標籤 |
| `IWebhookVerificationService.cs` | ⚠️ 只有類別 summary | 方法補 `<summary>`、`<param>`、`<returns>` |

### Models/
| 檔案 | 現況 | 需補項目 |
|---|---|---|
| `InboundMessage.cs` | ❌ 無任何註解 | 類別 `<summary>` + 各參數 `<param>` |
| `OutboundMessage.cs` | ❌ 無任何註解 | 類別 `<summary>` + 各參數 `<param>` |
| `MessageLogEntry.cs` | ❌ 無任何註解 | 類別 `<summary>` + 各參數 `<param>` |
| `SendMessageRequest.cs` | ❌ 無任何註解 | 類別 `<summary>` + 各參數 `<param>` |
| `WebhookTextMessageRequest.cs` | ❌ 無任何註解 | 類別 `<summary>` + 各參數 `<param>` |
| `WebhookVerifyRequest.cs` | ❌ 無任何註解 | 類別 `<summary>` + 各參數 `<param>` |
| `WebhookVerifyResult.cs` | ❌ 無任何註解 | 類別 `<summary>` + 各參數 `<param>` |
| `ChannelDefinition.cs` | ❌ 無任何註解 | 類別 `<summary>` + 各參數 `<param>` |
| `ChannelTypeDefinition.cs` | ❌ 無任何註解 | 類別 `<summary>` + 各參數 `<param>` |
| `ChannelConfigFieldDefinition.cs` | ❌ 無任何註解 | 類別 `<summary>` + 各參數 `<param>` |
| `RecentTargetInfo.cs` | ❌ 無任何註解 | 類別 `<summary>` + 各參數 `<param>` |
| `DeliveryStatus.cs` | ❌ 無任何註解 | 列舉 `<summary>` + 各值 |
| `MessageDirection.cs` | ❌ 無任何註解 | 列舉 `<summary>` + 各值 |
| `ChannelConfig.cs` | ✅ 已有 summary | 各屬性補 `<summary>` |
| `ChannelSettings.cs` | ✅ 已有 summary | 各屬性補 `<summary>` |
| `DeadLetterMessage.cs` | ✅ 已有 summary | 各參數補 `<param>` |

### Services/
| 檔案 | 現況 | 需補項目 |
|---|---|---|
| `ChannelSettingsService.cs` | ⚠️ 部分方法有 | 類別 `<summary>`、`GetAsync`/`SaveAsync`/`NormalizeConfig`/`NormalizeSettings`/`Rename` 方法補齊、行內註解補齊正規化邏輯 |
| `UnifiedMessageProcessor.cs` | ⚠️ 類別有 summary | `HandleInboundAsync`、`SendManualAsync` 方法補齊 `<summary>`/`<param>`/`<returns>`，複雜的 targetId 解析邏輯補行內註解 |

### Channels/
| 檔案 | 現況 | 需補項目 |
|---|---|---|
| `TelegramChannel.cs` | ✅ 類別有 summary | 各方法補 `<param>`/`<returns>`/`<exception>`，`SendAsync` 的 API 呼叫邏輯補行內註解 |
| `LineChannel.cs` | ✅ 類別有 summary | 同上 |
| `EmailChannel.cs` | ✅ 類別有 summary | 同上 |
| `NotificationService.cs` | ✅ 已有 | 補 `<param>`/`<exception>` |
| `WebhookVerificationService.cs` | ❌ 類別無 summary | 類別 `<summary>`、`VerifyAsync` 方法完整註解，各頻道驗證分支補行內註解 |

### Bus/
| 檔案 | 現況 | 需補項目 |
|---|---|---|
| `MessageBus.cs` | ✅ 類別有 summary | 各方法補 `<param>`/`<returns>` |
| `ChannelManager.cs` | ✅ 類別有 summary | `ExecuteAsync`、`ProcessMessageAsync`、`ExtractTargetDisplayName`、`GetRateLimiter` 各方法補齊，重試 + DLQ 流程補行內註解 |

### Stores/
| 檔案 | 現況 | 需補項目 |
|---|---|---|
| `InMemoryMessageLogStore.cs` | ❌ 無任何註解 | 類別 `<summary>`、各方法、佇列上限邏輯補行內註解 |
| `JsonChannelSettingsStore.cs` | ❌ 無任何註解 | 類別 `<summary>`、各方法、Legacy 反序列化邏輯補行內註解 |
| `RecentTargetStore.cs` | ❌ 無任何註解 | 類別 `<summary>`、各方法 |

### 其他
| 檔案 | 現況 | 需補項目 |
|---|---|---|
| `ChannelFactory.cs` | ✅ 已有 summary | 建構子補 `<param>`，`GetDefinitions` 補 `<returns>` |
| `ChannelSettingsResolver.cs` | ❌ 無任何註解 | 類別 `<summary>`、`FindSettings` 方法的多層匹配邏輯補行內註解、`LooksLikeChannelType` 補完整註解 |
| `DependencyInjection.cs` | ❌ 無任何註解 | 類別 `<summary>`、`AddMessageHubCore` 方法 `<summary>` |

## 三、行內註解（Inline Comments）需補強的複雜邏輯

| 檔案 | 方法 | 需註解的邏輯 |
|---|---|---|
| `ChannelSettingsResolver.cs` | `FindSettings` | 多層 fallback 匹配順序：精確 → normalized → prefix/suffix/contains → LooksLikeChannelType |
| `ChannelSettingsResolver.cs` | `LooksLikeChannelType` | 依頻道名稱反推 parameter key 的啟發式邏輯 |
| `ChannelSettingsService.cs` | `NormalizeConfig` | 為何要過濾空白 key + 重建 Dictionary |
| `ChannelSettingsService.cs` | `NormalizeSettings` | Legacy key 重新命名的原因與邏輯 |
| `UnifiedMessageProcessor.cs` | `SendManualAsync` | targetId 解析的三級 fallback（request → recentTarget → 失敗） |
| `ChannelManager.cs` | `ProcessMessageAsync` | 重試 → 成功紀錄 / 失敗 → DLQ + 失敗紀錄的完整流程 |
| `ChannelManager.cs` | `ExtractTargetDisplayName` | 反射取 Metadata 屬性的原因 |
| `JsonChannelSettingsStore.cs` | `LoadAsync` | 檔案不存在 → 建立預設 / 空白 → 空 config / 嘗試新格式 → 嘗試舊格式的 fallback 鏈 |
| `JsonChannelSettingsStore.cs` | `TryDeserializeLegacy` | Legacy 陣列格式到新 Dictionary 格式的轉換 |
| `InMemoryMessageLogStore.cs` | `AddAsync` | 為何設定 500 上限 + ConcurrentQueue 淘汰邏輯 |

---
 

## 四、驗證標準

每個 Phase 完成後須通過：
- [ ] `dotnet build` 零錯誤零警告
- [ ] `dotnet test` 全部通過（含既有 15 個測試）
- [ ] LSP diagnostics 無新增 error
- [ ] 既有 API 端點行為不變（功能回歸）

---

## 五、實施結果

本重構已於 2026-03-24 完成，變更摘要如下：

### 已完成項目

- [x] SRP 違反修正：`UnifiedMessageProcessor` 拆分為 `MessageCoordinator` (IMessageCoordinator) + `EchoMessageProcessor` (IMessageProcessor) — 已在先前的重構中完成
- [x] 非通訊職責從 Core 搬移至 Domain：ChannelSettingsService、JsonChannelSettingsStore、NotificationService、WebhookVerificationService
- [x] ChannelManager (BackgroundService) 搬移至新建的 MessageHub.Worker 專案
- [x] 刪除已被 SQLite 取代的記憶體儲存實作：InMemoryMessageLogStore、RecentTargetStore
- [x] Core DI 精簡為僅註冊通訊相關服務（Channels、MessageBus、MessageCoordinator、EchoMessageProcessor）
- [x] 所有 14 個測試通過、零建置錯誤、零警告

### 未實施項目

- ISP 違反（IChannelSettingsService 繼承 ICommonParameterProvider）：決定維持現狀
- XML 文件註解補齊（第二、三節）：本次重構範圍外，可另行處理