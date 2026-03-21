namespace MessageHub.Core;

/// <summary>
/// 頻道工廠 — 負責根據頻道名稱動態建立或取得對應的 IChannel 實例。
/// 對應規格文件中的 ChannelFactory。
/// </summary>
public sealed class ChannelFactory
{
    private readonly IReadOnlyDictionary<string, IChannel> _lookup;
    private readonly IReadOnlyList<ChannelDefinition> _definitions;

    public ChannelFactory(IEnumerable<IChannel> channels)
    {
        var channelList = channels.ToArray();
        _lookup = channelList.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
        _definitions = channelList
            .Select(ch => new ChannelDefinition(ch.Name, true, true, ch.Name))
            .ToArray();
    }

    /// <summary>根據輸入字串回傳對應的頻道物件，若不支援則拋出異常</summary>
    public IChannel GetChannel(string channel)
        => _lookup.TryGetValue(channel, out var client)
            ? client
            : throw new KeyNotFoundException($"找不到頻道：{channel}");

    public IReadOnlyList<ChannelDefinition> GetDefinitions() => _definitions;
}
