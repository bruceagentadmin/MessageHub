# 03 — MessageBus 訊息匯流排

> 本文件詳述 `Bus/` 資料夾下的 `MessageBus` 與 `ChannelManager`，這是整個系統的訊息傳遞骨幹。

---

## 架構概覽

```
                    ┌─────────────────────────────────────────┐
                    │              MessageBus                  │
                    │                                         │
  PublishOutbound ──▶  ┌──────────┐   ConsumeOutbound ──▶ ChannelManager
                    │  │ Outbound │                          │
                    │  │  Queue   │                          │
                    │  └──────────┘                          │
                    │                                         │
  PublishInbound ───▶  ┌──────────┐   ConsumeInbound ──▶ (未來擴展)
                    │  │ Inbound  │                          │
                    │  │  Queue   │                          │
                    │  └──────────┘                          │
                    │                                         │
  PublishDeadLetter─▶  ┌──────────┐   ConsumeDeadLetter ──▶ (監控儀表板)
                    │  │   DLQ    │                          │
                    │  │  Queue   │                          │
                    │  └──────────┘                          │
                    └─────────────────────────────────────────┘
```

---

## MessageBus（`Bus/MessageBus.cs`）

### 技術選型

使用 `System.Threading.Channels.Channel<T>` 作為底層佇列實作：

- **無界佇列** (`CreateUnbounded`)：不設容量上限，適合 POC 階段
- **多讀多寫** (`SingleReader = false, SingleWriter = false`)：允許多生產者與消費者並行存取
- **零外部相依**：僅使用 .NET BCL，不需 RabbitMQ / Kafka 等中介軟體

### 三條佇列

| 佇列 | 泛型型別 | 生產者 | 消費者 |
|------|---------|--------|--------|
| **Outbound** | `Channel<OutboundMessage>` | MessageCoordinator, NotificationService | ChannelManager |
| **Inbound** | `Channel<InboundMessage>` | （目前未使用，保留擴展）| （目前未使用）|
| **Dead Letter** | `Channel<DeadLetterMessage>` | ChannelManager | （監控儀表板/人工介入）|

### 監控屬性

```csharp
public int OutboundPendingCount => _outbound.Reader.Count;
public int InboundPendingCount  => _inbound.Reader.Count;
public int DeadLetterPendingCount => _deadLetter.Reader.Count;
```

這些屬性可供健康檢查或監控儀表板即時查詢佇列深度。

---

## ChannelManager（`Bus/ChannelManager.cs`）

### 角色

`ChannelManager` 是一個 `BackgroundService`（`IHostedService`），在應用程式啟動時自動啟動，持續監聽 Outbound 佇列並逐一處理訊息。

### 執行流程

```mermaid
flowchart TD
    A[啟動 ExecuteAsync] --> B{ConsumeOutboundAsync<br/>有訊息？}
    B -- 是 --> C[取得 Per-Channel SemaphoreSlim]
    C --> D[WaitAsync 等待進入]
    D --> E[ProcessMessageAsync]
    E --> F[ChannelFactory.GetChannel]
    F --> G[ChannelSettingsService.GetAsync]
    G --> H[ChannelSettingsResolver.FindSettings]
    H --> I{頻道已啟用？}
    I -- 否 --> J[拋出 InvalidOperationException]
    I -- 是 --> K[RetryPipeline.ExecuteAsync]
    K --> L[IChannel.SendAsync]
    L --> M{發送成功？}
    M -- 是 --> N[記錄成功日誌]
    M -- 否 --> O[PublishDeadLetterAsync]
    O --> P[記錄失敗日誌]
    J --> O
    N --> Q[Release SemaphoreSlim]
    P --> Q
    Q --> B
    B -- 取消 --> R[結束]
```

### Per-Channel 速率限制

```csharp
private readonly ConcurrentDictionary<string, SemaphoreSlim> _rateLimiters = new(...);

private SemaphoreSlim GetRateLimiter(string channel)
    => _rateLimiters.GetOrAdd(channel, _ => new SemaphoreSlim(1, 1));
```

- 每個頻道名稱對應一個 `SemaphoreSlim(1, 1)`（互斥鎖語意）
- **效果**：同一頻道的訊息串行處理，不同頻道可並行
- **目的**：避免觸發平台 API 的速率限制（Rate Limit）

### 重試與死信流程

```mermaid
sequenceDiagram
    participant CM as ChannelManager
    participant RP as IRetryPipeline
    participant CH as IChannel
    participant BUS as MessageBus
    participant LOG as MessageLogStore

    CM->>RP: ExecuteAsync(SendAsync lambda)
    loop 最多 3 次（指數退避）
        RP->>CH: SendAsync(chatId, message, settings)
        alt 成功
            CH-->>RP: 完成
            RP-->>CM: 成功
            CM->>LOG: AddAsync(成功日誌)
        else 失敗
            CH-->>RP: 拋出例外
            RP->>RP: 等待退避時間後重試
        end
    end
    Note over RP: 3 次均失敗
    RP-->>CM: 拋出最後一次例外
    CM->>BUS: PublishDeadLetterAsync(deadLetter)
    CM->>LOG: AddAsync(失敗日誌)
```

### Metadata 處理

`ChannelManager` 使用反射從 `OutboundMessage.Metadata` 提取 `TargetDisplayName`：

```csharp
private static string? ExtractTargetDisplayName(object? metadata)
{
    var property = metadata?.GetType().GetProperty("TargetDisplayName");
    return property?.GetValue(metadata)?.ToString();
}
```

這是刻意的設計：`Metadata` 型別為 `object?`，不同頻道可攜帶不同欄位，保持彈性。

---

## 生命週期

| 元件 | DI 生命週期 | 說明 |
|------|-------------|------|
| `MessageBus` | Singleton | 全域唯一佇列實例 |
| `ChannelManager` | HostedService | 由 .NET 泛型主機管理啟停 |

```csharp
services.AddSingleton<MessageBus>();
services.AddSingleton<IMessageBus>(sp => sp.GetRequiredService<MessageBus>());
services.AddHostedService<ChannelManager>();
```

> `IMessageBus` 介面指向同一個 `MessageBus` Singleton 實例，確保所有注入點共用同一組佇列。
