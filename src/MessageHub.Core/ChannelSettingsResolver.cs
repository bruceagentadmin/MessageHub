using MessageHub.Core.Models;

namespace MessageHub.Core;

/// <summary>
/// 頻道設定解析器 — 靜態輔助類別，負責從 <see cref="ChannelConfig"/> 中
/// 以模糊匹配策略找出最符合指定頻道名稱的 <see cref="ChannelSettings"/>。
/// <para>
/// 匹配策略優先順序（由精確到模糊）：
/// <list type="number">
///   <item>字典直接查找（精確鍵值比對）</item>
///   <item>不區分大小寫的完整名稱比對</item>
///   <item>前綴比對（鍵值以 <c>{頻道名}_{suffix}</c> 形式開頭）</item>
///   <item>後綴比對（鍵值以 <c>{prefix}_{頻道名}</c> 形式結尾）</item>
///   <item>包含比對（鍵值含有頻道名稱子字串）</item>
///   <item>依據頻道特徵參數鍵名推斷頻道類型（<see cref="LooksLikeChannelType"/>）</item>
/// </list>
/// </para>
/// </summary>
public static class ChannelSettingsResolver
{
    /// <summary>
    /// 從 <paramref name="config"/> 中找出最符合 <paramref name="channelName"/> 的頻道設定。
    /// 依照由精確到模糊的策略進行比對，找到第一個匹配即回傳。
    /// </summary>
    /// <param name="config">包含所有頻道設定的配置物件。</param>
    /// <param name="channelName">要查找的頻道名稱，例如 "telegram"、"line"。</param>
    /// <returns>
    /// 找到匹配時回傳對應的 <see cref="ChannelSettings"/>；
    /// 若沒有任何匹配則回傳 <c>null</c>。
    /// </returns>
    public static ChannelSettings? FindSettings(ChannelConfig config, string channelName)
    {
        // 第一優先：直接以字典索引查找（最快路徑，字典本身已不區分大小寫）
        if (config.Channels.TryGetValue(channelName, out var direct))
        {
            return direct;
        }

        // 第二優先以後：去除前後空白後，以多種模糊策略進行比對
        var normalized = channelName.Trim();
        var match = config.Channels.FirstOrDefault(x =>
            // 策略 2：完整名稱比對（不區分大小寫）
            x.Key.Equals(normalized, StringComparison.OrdinalIgnoreCase)
            // 策略 3：前綴比對，例如 key = "telegram_prod" 匹配 channelName = "telegram"
            || x.Key.StartsWith(normalized + "_", StringComparison.OrdinalIgnoreCase)
            // 策略 4：後綴比對，例如 key = "prod_telegram" 匹配 channelName = "telegram"
            || x.Key.EndsWith("_" + normalized, StringComparison.OrdinalIgnoreCase)
            // 策略 5：包含比對，例如 key = "my-telegram-bot" 匹配 channelName = "telegram"
            || x.Key.Contains(normalized, StringComparison.OrdinalIgnoreCase)
            // 策略 6：依特徵參數鍵名推斷頻道類型（用於鍵名完全無法對應的情況）
            || LooksLikeChannelType(x.Key, x.Value, normalized));

        // 若所有策略均無匹配（match.Key 為 null 或空白字串），回傳 null
        return string.IsNullOrWhiteSpace(match.Key) ? null : match.Value;
    }

    /// <summary>
    /// 根據頻道設定的鍵名與參數名稱，推斷其是否對應指定的頻道類型。
    /// 當頻道鍵名與標準名稱不符但設定內容含有頻道特徵時使用此方法作為最後回退手段。
    /// <para>
    /// 特徵判斷邏輯：
    /// <list type="bullet">
    ///   <item>telegram：鍵名或參數含有 "telegram" 或 "bottoken"</item>
    ///   <item>line：鍵名或參數含有 "line"、"channelaccesstoken" 或 "channelsecret"</item>
    ///   <item>email：鍵名或參數含有 "email"、"smtp" 或 "host"</item>
    /// </list>
    /// </para>
    /// </summary>
    /// <param name="key">頻道設定在字典中的鍵值。</param>
    /// <param name="settings">對應的頻道設定物件，用於取得參數鍵名集合。</param>
    /// <param name="channelName">要比對的目標頻道類型名稱（已轉為小寫）。</param>
    /// <returns>若推斷為指定頻道類型則回傳 <c>true</c>；否則回傳 <c>false</c>。</returns>
    private static bool LooksLikeChannelType(string key, ChannelSettings settings, string channelName)
    {
        // 將鍵名與所有參數鍵名合併為一個小寫字串，方便一次性搜尋
        var haystack = string.Join(' ', new[]
        {
            key,
            string.Join(' ', settings.Parameters.Keys)
        }).ToLowerInvariant();

        // 使用 switch expression 依頻道類型比對特徵關鍵字
        return channelName.ToLowerInvariant() switch
        {
            "telegram" => haystack.Contains("telegram") || haystack.Contains("bottoken"),
            "line" => haystack.Contains("line") || haystack.Contains("channelaccesstoken") || haystack.Contains("channelsecret"),
            "email" => haystack.Contains("email") || haystack.Contains("smtp") || haystack.Contains("host"),
            // 不支援的頻道類型無法推斷，一律回傳 false
            _ => false
        };
    }
}
