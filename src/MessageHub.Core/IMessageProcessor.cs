namespace MessageHub.Core;

/// <summary>
/// 訊息處理介面 — 定義對接收到的訊息進行商業邏輯處理的入口。
/// 對應規格文件中的 IMessageProcessor。
/// </summary>
public interface IMessageProcessor
{
    /// <summary>接收 InboundMessage 並回傳處理後的回覆文字</summary>
    Task<string> ProcessAsync(InboundMessage message, CancellationToken cancellationToken = default);
}
