using MessageHub.Core.Models;

namespace MessageHub.Core;

/// <summary>
/// 頻道工廠 — 負責根據頻道名稱動態建立或取得對應的 <see cref="IChannel"/> 實例。
/// 在建構時接收所有已註冊的 <see cref="IChannel"/> 實作，建立以頻道名稱為鍵的查找字典，
/// 供執行期間快速依名稱取得對應的頻道物件。
/// 對應規格文件中的 ChannelFactory。
/// </summary>
public sealed class ChannelFactory
{
    /// <summary>以頻道名稱（不區分大小寫）為鍵的頻道查找字典。</summary>
    private readonly IReadOnlyDictionary<string, IChannel> _lookup;

    /// <summary>所有已註冊頻道的定義清單，供列出可用頻道時使用。</summary>
    private readonly IReadOnlyList<ChannelDefinition> _definitions;

    /// <summary>
    /// 初始化 <see cref="ChannelFactory"/>，並從 DI 容器注入的頻道集合建立查找索引。
    /// </summary>
    /// <param name="channels">
    /// 由相依性注入容器提供的所有 <see cref="IChannel"/> 實作集合
    /// （Telegram、Line、Email 等）。
    /// </param>
    public ChannelFactory(IEnumerable<IChannel> channels)
    {
        var channelList = channels.ToArray();
        // 建立以頻道名稱為鍵（不區分大小寫）的快速查找字典
        _lookup = channelList.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
        // 為每個頻道建立 ChannelDefinition，預設啟用且支援收發（POC 階段固定為 true）
        _definitions = channelList
            .Select(ch => new ChannelDefinition(ch.Name, true, true, ch.Name))
            .ToArray();
    }

    /// <summary>
    /// 根據輸入的頻道名稱回傳對應的 <see cref="IChannel"/> 物件。
    /// 名稱比較不區分大小寫（例如 "telegram"、"Telegram"、"TELEGRAM" 均可匹配）。
    /// </summary>
    /// <param name="channel">頻道名稱，例如 "telegram"、"line"、"email"。</param>
    /// <returns>對應的 <see cref="IChannel"/> 實作實例。</returns>
    /// <exception cref="KeyNotFoundException">
    /// 當指定的頻道名稱在已註冊的頻道中找不到對應實作時拋出。
    /// </exception>
    public IChannel GetChannel(string channel)
        => _lookup.TryGetValue(channel, out var client)
            ? client
            : throw new KeyNotFoundException($"找不到頻道：{channel}");

    /// <summary>
    /// 取得所有已註冊頻道的定義清單，包含名稱、啟用狀態及支援的操作類型。
    /// 供控制中心 API 列出可用頻道時呼叫。
    /// </summary>
    /// <returns>所有已註冊頻道的 <see cref="ChannelDefinition"/> 唯讀清單。</returns>
    public IReadOnlyList<ChannelDefinition> GetDefinitions() => _definitions;
}
