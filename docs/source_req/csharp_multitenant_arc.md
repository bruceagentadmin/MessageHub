# 多租戶 C# 頻道系統架構設計 (最終階段預留版)

此架構設計基於 .NET 8/9，採用 **Strategy Pattern** (策略模式) 處理頻道，並使用 **Dependency Injection** (依賴注入) 與子容器概念實現多租戶隔離。

## 1. 核心模型與介面

### 頻道配置 (Channel Configuration) - 存於 DB JSON 欄位
```csharp
public class ChannelConfig
{
    public Guid TenantId { get; set; }
    public string Name { get; set; }
    public Dictionary<string, ChannelSettings> Channels { get; set; } = new();
}

public class ChannelSettings
{
    public bool Enabled { get; set; }
    public string ProviderType { get; set; } // "Line", "Telegram", "Email"
    public Dictionary<string, string> Parameters { get; set; } = new(); // 存 Token, Secret 等
}
```

### 訊息定義 (Message Objects)
```csharp
public record InboundMessage(
    string TenantId, 
    string ChannelName, 
    string SenderId, 
    string ChatId, 
    string Content, 
    object OriginalPayload);

public record OutboundMessage(
    string Content, 
    string TargetId = null);
```

### 頻道介面 (IChannel)
```csharp
public interface IChannel
{
    string Name { get; }
    Task<InboundMessage> ParseRequestAsync(HttpRequest request);
    Task SendAsync(string chatId, OutboundMessage message);
}
```

### 頻道配置定義 (Channel Configuration)
透過系統內建的 `ICommonParameterProvider` 獲取。介面定義如下：

```csharp
public interface ICommonParameterProvider
{
    Task<T> GetParameterByKeyAsync<T>(string key);
}
```
```

### 頻道實作範例 (LineChannel)
```csharp
public class LineChannel : IChannel
{
    public string Name => "Line";

    public async Task<InboundMessage> ParseRequestAsync(HttpRequest request)
    {
        using var reader = new StreamReader(request.Body);
        var body = await reader.ReadToEndAsync();
        
        // 1. 驗證 Signature (省略細節)
        // 2. 解析 Line Webhook JSON
        var lineEvent = JsonSerializer.Deserialize<LineWebhookEvent>(body);
        
        return new InboundMessage(
            TenantId: "...", // 從 Context 或路由取得
            ChannelName: Name,
            SenderId: lineEvent.Source.UserId,
            ChatId: lineEvent.Source.UserId,
            Content: lineEvent.Message.Text,
            OriginalPayload: lineEvent
        );
    }

    public async Task SendAsync(string chatId, OutboundMessage message)
    {
        // 呼叫 Line Messaging API
        Console.WriteLine($"[Line API] 傳送至 {chatId}: {message.Content}");
    }
}
```

### 頻道工廠 (ChannelFactory)
```csharp
public class ChannelFactory
{
    private readonly IEnumerable<IChannel> _channels;
    public ChannelFactory(IEnumerable<IChannel> channels) => _channels = channels;

    public IChannel GetChannel(string name) => 
        _channels.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
        ?? throw new NotSupportedException($"Channel {name} is not supported.");
}
```

## 3. 處理引擎與主動通知

### 訊息處理器 (Processor)
不再區分階段，統一由 `IMessageProcessor` 定義。

```csharp
public interface IMessageProcessor
{
    Task<string> ProcessAsync(InboundMessage message);
}

public class UnifiedMessageProcessor : IMessageProcessor
{
    public async Task<string> ProcessAsync(InboundMessage message)
    {
        // 這裡實作邏輯：可以包含關鍵字判斷、呼叫 AI、或是單純回覆
        return $"[系統] 您好，已收到訊息：{message.Content}";
    }
}
```

### 主動通知服務 (NotificationService)
用於系統定時發送通知。

```csharp
public interface INotificationService
{
    Task SendGlobalNotificationAsync(Guid tenantId, string channelName, string message);
}

public class NotificationService : INotificationService
{
    private readonly ChannelFactory _factory;
    private readonly ICommonParameterProvider _parameterProvider;

    public NotificationService(ChannelFactory factory, ICommonParameterProvider parameterProvider)
    {
        _factory = factory;
        _parameterProvider = parameterProvider;
    }

    public async Task SendGlobalNotificationAsync(Guid tenantId, string channelName, string message)
    {
        // 透過 Provider 取得配置
        var config = await _parameterProvider.GetParameterByKeyAsync<ChannelConfig>("ChannelConfig");
        if (config == null || !config.Channels.TryGetValue(channelName, out var settings)) return;

        var channel = _factory.GetChannel(channelName);
        // 這邊需要定位通知對象，例如預設群組或特定管理員
        var targetChatId = settings.Parameters.GetValueOrDefault("NotificationTargetId");
        
        await channel.SendAsync(targetChatId, new OutboundMessage(message));
    }
}
```

## 4. Webhook 進入點 (Controller)

```csharp
[ApiController]
[Route("api/webhooks/{channelName}/{tenantId}")]
public class WebhookController : ControllerBase
{
    private readonly ChannelFactory _channelFactory;
    private readonly ICommonParameterProvider _parameterProvider;
    private readonly IMessageProcessor _processor;

    public WebhookController(ChannelFactory factory, ICommonParameterProvider parameterProvider, IMessageProcessor processor)
    {
        _channelFactory = factory;
        _parameterProvider = parameterProvider;
        _processor = processor;
    }

    [HttpPost]
    public async Task<IActionResult> Handle(string channelName, Guid tenantId)
    {
        // 1. 透過 ParameterProvider 獲取頻道配置
        var channelConfig = await _parameterProvider.GetParameterByKeyAsync<ChannelConfig>("ChannelConfig");
        if (channelConfig == null || !channelConfig.Channels.ContainsKey(channelName)) return NotFound();

        // 2. 取得對應頻道解析器
        var channel = _channelFactory.GetChannel(channelName);
        
        // 3. 解析進站訊息
        var inboundMsg = await channel.ParseRequestAsync(Request);

        // 4. 交給處理引擎
        var responseText = await _processor.ProcessAsync(inboundMsg);

        // 5. 送回回覆
        await channel.SendAsync(inboundMsg.ChatId, new OutboundMessage(responseText));

        return Ok();
    }
}
```

## 5. 未來擴展策略

- **新增頻道**: 實作新的 `IChannel` (如 `SftpChannel`) 並註冊到 DI。
- **改進處理邏輯**: 
    - 中期：實作 `KeywordProcessor : IMessageProcessor`。
    - 後期：實作 `AiProcessor : IMessageProcessor`，並在 DI 中做切換。
- **多維度配置獲取**: 透過 `ICommonParameterProvider`，系統可以靈活地根據身份（租戶、用戶等）讀取不同的 `ChannelConfig`。
