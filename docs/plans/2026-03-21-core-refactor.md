# MessageHub.Core 重構實作計畫

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** 建立 `MessageHub.Core` 專案，將散落在各專案的核心介面與模型移入，按規格文件 (`CHANNEL_SYSTEM_CS_MULTITENANT.md` + `MESSAGE_BUS_ARCHITECTURE_CS.md`) 重新命名與組織，並實作 MessageBus + ChannelManager。最終所有 15 個現有測試必須通過。

**Architecture:** 新增 `MessageHub.Core` 專案作為核心層，取代原本 `MessageHub.Domain` 的角色並整合 `MessageHub.Application` 中的介面定義。Core 放置所有規格定義的介面 (`IChannel`, `ICommonParameterProvider`, `IMessageProcessor`, `INotificationService`, `IMessageBus`) 及資料模型 (`ChannelConfig`, `ChannelSettings`, `InboundMessage`, `OutboundMessage`)。Infrastructure 實作具體的 Channel、Bus、Manager 等。Application 保留上層協調邏輯。

**Tech Stack:** .NET 8, C#, xUnit, System.Threading.Channels

---

## 名稱對照表 (舊名 → 新名)

| 舊名 | 位置 | 新名 | 新位置 |
|---|---|---|---|
| `IChannelClient` | Application/Contracts.cs | `IChannel` | Core/IChannel.cs |
| `IChannelRegistry` | Application/Contracts.cs | `ChannelFactory` (class) | Core/ChannelFactory.cs |
| `IMessageOrchestrator` | Application/Contracts.cs | `IMessageProcessor` | Core/IMessageProcessor.cs |
| `IMessageLogStore` | Application/Contracts.cs | `IMessageLogStore` | Core/IMessageLogStore.cs |
| `IRecentTargetStore` | Application/Contracts.cs | `IRecentTargetStore` | Core/IRecentTargetStore.cs |
| `IChannelSettingsService` | Application/ChannelSettingsContracts.cs | `ICommonParameterProvider` | Core/ICommonParameterProvider.cs |
| `IChannelSettingsStore` | Application/ChannelSettingsContracts.cs | `IChannelSettingsStore` | Core/IChannelSettingsStore.cs |
| `IWebhookVerificationService` | Application/WebhookVerificationContracts.cs | `IWebhookVerificationService` | Core/IWebhookVerificationService.cs |
| (新增) | — | `INotificationService` | Core/INotificationService.cs |
| (新增) | — | `IMessageBus` | Core/IMessageBus.cs |
| `InboundMessage` | Domain/Models.cs | `InboundMessage` | Core/Models/InboundMessage.cs |
| `OutboundMessage` | Domain/Models.cs | `OutboundMessage` | Core/Models/OutboundMessage.cs |
| `MessageLogEntry` | Domain/Models.cs | `MessageLogEntry` | Core/Models/MessageLogEntry.cs |
| `SendMessageRequest` | Domain/Models.cs | `SendMessageRequest` | Core/Models/SendMessageRequest.cs |
| `WebhookTextMessageRequest` | Domain/Models.cs | `WebhookTextMessageRequest` | Core/Models/WebhookTextMessageRequest.cs |
| `ChannelDefinition` | Domain/Models.cs | `ChannelDefinition` | Core/Models/ChannelDefinition.cs |
| `MessageDirection` | Domain/Models.cs | `MessageDirection` | Core/Models/MessageDirection.cs |
| `DeliveryStatus` | Domain/Models.cs | `DeliveryStatus` | Core/Models/DeliveryStatus.cs |
| `ChannelSettingsDocument` | Domain/ChannelSettingsModels.cs | `ChannelConfig` | Core/Models/ChannelConfig.cs |
| `ChannelSettingsItem` | Domain/ChannelSettingsModels.cs | `ChannelSettings` | Core/Models/ChannelSettings.cs |
| `ChannelConfigFieldDefinition` | Domain/ChannelSettingsModels.cs | `ChannelConfigFieldDefinition` | Core/Models/ChannelConfigFieldDefinition.cs |
| `ChannelTypeDefinition` | Domain/ChannelSettingsModels.cs | `ChannelTypeDefinition` | Core/Models/ChannelTypeDefinition.cs |
| `WebhookVerifyRequest` | Domain/WebhookVerificationModels.cs | `WebhookVerifyRequest` | Core/Models/WebhookVerifyRequest.cs |
| `WebhookVerifyResult` | Domain/WebhookVerificationModels.cs | `WebhookVerifyResult` | Core/Models/WebhookVerifyResult.cs |
| `RecentTargetInfo` | Domain/RecentTargetModels.cs | `RecentTargetInfo` | Core/Models/RecentTargetInfo.cs |
| `ChannelRegistry` | Infrastructure/FakeChannels.cs | `ChannelFactory` | Core/ChannelFactory.cs |
| `MessageOrchestrator` | Application/MessageOrchestrator.cs | `UnifiedMessageProcessor` | Application/UnifiedMessageProcessor.cs |
| `ChannelSettingsService` | Application/ChannelSettingsService.cs | `CommonParameterProvider` | Infrastructure/CommonParameterProvider.cs |
| (新增) | — | `MessageBus` | Infrastructure/MessageBus.cs |
| (新增) | — | `ChannelManager` | Infrastructure/ChannelManager.cs |
| (新增) | — | `NotificationService` | Infrastructure/NotificationService.cs |

## 介面簽名變更對照

### IChannel (原 IChannelClient)
```csharp
// 舊
public interface IChannelClient {
    string Name { get; }
    Task<InboundMessage> ParseAsync(string tenantId, WebhookTextMessageRequest request, CancellationToken ct);
    Task<MessageLogEntry> SendAsync(OutboundMessage message, CancellationToken ct);
}

// 新 (依規格)
public interface IChannel {
    string Name { get; }
    Task<InboundMessage> ParseRequestAsync(HttpRequest request, string tenantId, CancellationToken ct);
    Task SendAsync(string chatId, OutboundMessage message, ChannelSettings settings, CancellationToken ct);
}
```

**注意**: 規格要求 `ParseRequestAsync` 接收 raw HttpRequest，但目前 POC 階段用的是已解析的 `WebhookTextMessageRequest`。為了保持測試通過且不破壞現有流程，我們保留一個接受 `WebhookTextMessageRequest` 的重載。

### 實際採用的 IChannel 簽名 (兼容版)
```csharp
public interface IChannel {
    string Name { get; }
    Task<InboundMessage> ParseRequestAsync(string tenantId, WebhookTextMessageRequest request, CancellationToken ct = default);
    Task<MessageLogEntry> SendAsync(string chatId, OutboundMessage message, ChannelSettings? settings = null, CancellationToken ct = default);
}
```

### ICommonParameterProvider (原 IChannelSettingsService)
```csharp
public interface ICommonParameterProvider {
    Task<T?> GetParameterByKeyAsync<T>(string key, CancellationToken ct = default) where T : class;
}
```

### IMessageProcessor (原 IMessageOrchestrator)
```csharp
public interface IMessageProcessor {
    Task<string> ProcessAsync(InboundMessage message, CancellationToken ct = default);
}
```

**注意**: 規格中 IMessageProcessor 只回傳 string。但現有 MessageOrchestrator 做了更多事 (log、auto-reply、SendManual 等)。我們將 UnifiedMessageProcessor 保留完整功能，同時實作 IMessageProcessor 介面。

### INotificationService (新增)
```csharp
public interface INotificationService {
    Task SendGlobalNotificationAsync(string tenantId, string channel, string message, CancellationToken ct = default);
}
```

### IMessageBus (新增)
```csharp
public interface IMessageBus {
    ValueTask PublishOutboundAsync(OutboundMessage message, CancellationToken ct = default);
    IAsyncEnumerable<OutboundMessage> ConsumeOutboundAsync(CancellationToken ct);
}
```

### ChannelConfig (原 ChannelSettingsDocument)
```csharp
// 改名但結構不變 — 保持 JSON 序列化相容
public sealed class ChannelConfig {
    public Guid TenantId { get; set; }
    public List<ChannelSettings> Channels { get; set; } = new();
}
```

### ChannelSettings (原 ChannelSettingsItem)
```csharp
public sealed class ChannelSettings {
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public Dictionary<string, string> Parameters { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
```
**注意**: 規格裡用 `Parameters`；原本用 `Config`。需確保 JSON 檔案相容。

---

## 任務列表

### Task 1: 建立 MessageHub.Core 專案 + 加入 Solution

**Files:**
- Create: `src/MessageHub.Core/MessageHub.Core.csproj`
- Modify: `MessageHub.slnx`

**Step 1: 建立專案**
```bash
dotnet new classlib -n MessageHub.Core -o src/MessageHub.Core --framework net8.0
```

**Step 2: 刪除預設 Class1.cs**
```bash
rm src/MessageHub.Core/Class1.cs
```

**Step 3: 加入 Solution**
```bash
dotnet sln MessageHub.slnx add src/MessageHub.Core/MessageHub.Core.csproj --solution-folder src
```

**Step 4: 設定專案引用**
- `MessageHub.Application.csproj` 改引用 Core (取代 Domain)
- `MessageHub.Infrastructure.csproj` 改引用 Core (取代 Domain + Application)
- `MessageHub.Api.csproj` 加引用 Core
- `MessageHub.Tests.csproj` 加引用 Core
- `MessageHub.Domain.csproj` 保留但清空 (後續可移除)

**Step 5: 確認 build**
```bash
dotnet build
```

### Task 2: Core 模型 — 搬移並重新命名 Models

將 Domain 中所有 model 搬到 Core，使用新名稱，namespace 為 `MessageHub.Core`。每個類別一個檔案。

**Files to create in `src/MessageHub.Core/Models/`:**
- `InboundMessage.cs` — 同現有定義 (保留 TenantId, Channel, SenderId, Content, ReceivedAt, RawPayload)
- `OutboundMessage.cs` — 以 CHANNEL_SYSTEM 為主 (保留 TenantId, Channel, TargetId, Content, CreatedAt, TriggeredBy)
- `MessageLogEntry.cs`
- `SendMessageRequest.cs`
- `WebhookTextMessageRequest.cs`
- `ChannelDefinition.cs`
- `MessageDirection.cs`
- `DeliveryStatus.cs`
- `ChannelConfig.cs` (原 ChannelSettingsDocument，加 TenantId 欄位)
- `ChannelSettings.cs` (原 ChannelSettingsItem，Config → Parameters)
- `ChannelConfigFieldDefinition.cs`
- `ChannelTypeDefinition.cs`
- `WebhookVerifyRequest.cs`
- `WebhookVerifyResult.cs`
- `RecentTargetInfo.cs`

**關鍵改名:**
- `ChannelSettingsDocument` → `ChannelConfig` (加 `TenantId` 屬性)
- `ChannelSettingsItem` → `ChannelSettings` (`Config` → `Parameters`)
- Namespace: `MessageHub.Domain` → `MessageHub.Core`

### Task 3: Core 介面 — 定義規格要求的介面

**Files to create in `src/MessageHub.Core/`:**
- `IChannel.cs` (原 IChannelClient)
- `ICommonParameterProvider.cs` (新)
- `IMessageProcessor.cs` (新)
- `INotificationService.cs` (新)
- `IMessageBus.cs` (新)
- `IMessageLogStore.cs` (原在 Application/Contracts.cs)
- `IRecentTargetStore.cs` (原在 Application/Contracts.cs)
- `IChannelSettingsStore.cs` (原在 Application/ChannelSettingsContracts.cs)
- `IWebhookVerificationService.cs` (原在 Application/WebhookVerificationContracts.cs)

**Files to create in `src/MessageHub.Core/`:**
- `ChannelFactory.cs` (原 ChannelRegistry/IChannelRegistry 合併)

### Task 4: 更新 Application 層

**Files:**
- Rename: `MessageOrchestrator.cs` → `UnifiedMessageProcessor.cs`
- Update: `ChannelSettingsService.cs` → 改用新的 `ChannelConfig`/`ChannelSettings` 型別
- Delete: `Contracts.cs` (介面已移到 Core)
- Delete: `ChannelSettingsContracts.cs` (已移到 Core)
- Delete: `WebhookVerificationContracts.cs` (已移到 Core)
- Delete: `Class1.cs`

**UnifiedMessageProcessor** 同時實作 `IMessageProcessor` (規格) 且保留現有的完整功能介面。

### Task 5: 更新 Infrastructure 層

**Files:**
- Update: `FakeChannels.cs` 中的 Channel 實作 → 改實作 `IChannel` 介面
- Update: `DependencyInjection.cs` → 使用新介面名稱
- Update: 所有 using → `MessageHub.Core`
- Create: `Infrastructure/MessageBus.cs`
- Create: `Infrastructure/ChannelManager.cs`
- Create: `Infrastructure/NotificationService.cs`
- Create: `Infrastructure/CommonParameterProvider.cs`
- Delete: `Class1.cs`

### Task 6: 更新 Api 層

**Files:**
- Update: 所有 Controller → 使用新介面名稱
- Update: `Program.cs` → using 更新
- Delete: `WeatherForecast.cs`, `WeatherForecastController.cs`

### Task 7: 更新 Tests

- Update 所有 test 檔案中的 using 和型別名稱
- `IChannelClient` → `IChannel`
- `IChannelRegistry` → `ChannelFactory`
- `IChannelSettingsService` → `ICommonParameterProvider`
- `ChannelSettingsDocument` → `ChannelConfig`
- `ChannelSettingsItem` → `ChannelSettings`
- `Config` → `Parameters`
- `IMessageOrchestrator` → 使用 `UnifiedMessageProcessor` 的完整介面
- 新增 `MessageBus` 測試
- 新增 `ChannelManager` 測試
- 新增 `NotificationService` 測試

### Task 8: 清理 Domain 專案

- 清空所有檔案 (或移除整個專案)
- 如果移除專案，需更新 slnx

### Task 9: 驗證

```bash
dotnet build
dotnet test
```

所有 15 個現有測試 + 新增測試必須通過。
