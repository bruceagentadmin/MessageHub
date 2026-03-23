using MessageHub.Core;
using MessageHub.Core.Models;

namespace MessageHub.Core.Services;

/// <summary>
/// 頻道設定服務 — 提供頻道設定的讀取、儲存與正規化功能，
/// 同時實作 <see cref="IChannelSettingsService"/> 與 <see cref="ICommonParameterProvider"/> 介面。
/// <para>
/// 主要職責：
/// <list type="bullet">
///   <item>委派 <see cref="IChannelSettingsStore"/> 進行 JSON 持久化讀寫</item>
///   <item>每次讀取或儲存時，自動執行設定正規化（去除空白鍵值、重命名舊版參數鍵名）</item>
///   <item>提供各頻道類型的欄位定義清單（<see cref="ChannelTypeDefinition"/>）</item>
///   <item>透過 <see cref="ICommonParameterProvider"/> 讓其他服務以鍵值方式取得 <see cref="ChannelConfig"/></item>
/// </list>
/// </para>
/// </summary>
public sealed class ChannelSettingsService(IChannelSettingsStore store) : IChannelSettingsService, ICommonParameterProvider
{
    /// <summary>
    /// 各頻道類型的欄位定義清單，描述每個頻道需要哪些設定欄位、是否必填、是否為敏感資訊。
    /// 此清單供前端動態產生設定表單使用。
    /// </summary>
    private static readonly IReadOnlyList<ChannelTypeDefinition> Definitions =
    [
        // LINE 頻道：需要 ChannelAccessToken（必填敏感）與 ChannelSecret（POC 可留空）
        new(
            "Line",
            "LINE",
            [
                new ChannelConfigFieldDefinition("ChannelAccessToken", "Channel Access Token", "輸入 LINE channel access token", true, true),
                new ChannelConfigFieldDefinition("ChannelSecret", "Channel Secret（POC 可留空）", "若目前是 dev tunnel / POC，可先留空", false, true),
                new ChannelConfigFieldDefinition("WebhookUrl", "Webhook URL", "例如 https://xxxxx.devtunnels.ms/api/line/webhook", false),
                new ChannelConfigFieldDefinition("WebhookMode", "Webhook 模式", "例如 devtunnel / production", false)
            ]),
        // Telegram 頻道：需要 BotToken（必填敏感）
        new(
            "Telegram",
            "Telegram",
            [
                new ChannelConfigFieldDefinition("BotToken", "Bot Token", "輸入 Telegram bot token", true, true),
                new ChannelConfigFieldDefinition("WebhookUrl", "Webhook URL", "例如 https://xxxxx.devtunnels.ms/api/telegram/webhook", false),
                new ChannelConfigFieldDefinition("WebhookMode", "Webhook 模式", "例如 devtunnel / production", false)
            ]),
        // Email 頻道：需要 SMTP 連線資訊（Host、Port、Username、Password）
        new(
            "Email",
            "Email",
            [
                new ChannelConfigFieldDefinition("Host", "SMTP Host", "例如 smtp.gmail.com", true),
                new ChannelConfigFieldDefinition("Port", "SMTP Port", "例如 587", true),
                new ChannelConfigFieldDefinition("Username", "帳號", "例如 service@example.com", true),
                new ChannelConfigFieldDefinition("Password", "密碼", "輸入 SMTP 密碼", true, true)
            ])
    ];

    /// <inheritdoc />
    /// <summary>
    /// 依鍵值取得通用設定參數，供 <see cref="ICommonParameterProvider"/> 介面使用。
    /// 目前支援的鍵值：
    /// <list type="bullet">
    ///   <item><c>"ChannelConfig"</c>：回傳完整的 <see cref="ChannelConfig"/> 物件。</item>
    /// </list>
    /// 其他鍵值一律回傳 <c>null</c>。
    /// </summary>
    /// <typeparam name="T">期望的回傳型別，必須為參考型別。</typeparam>
    /// <param name="key">參數鍵名，目前支援 <c>"ChannelConfig"</c>。</param>
    /// <param name="cancellationToken">非同步取消權杖。</param>
    /// <returns>
    /// 鍵值匹配且型別相符時回傳對應值；否則回傳 <c>null</c>。
    /// </returns>
    public async Task<T?> GetParameterByKeyAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
    {
        if (key == "ChannelConfig" && typeof(T) == typeof(ChannelConfig))
        {
            var config = await GetAsync(cancellationToken);
            return config as T;
        }

        return null;
    }

    /// <inheritdoc />
    /// <summary>
    /// 從儲存層載入頻道設定，並執行正規化處理後回傳。
    /// </summary>
    /// <param name="cancellationToken">非同步取消權杖。</param>
    /// <returns>正規化後的 <see cref="ChannelConfig"/>。</returns>
    public async Task<ChannelConfig> GetAsync(CancellationToken cancellationToken = default)
    {
        var config = await store.LoadAsync(cancellationToken);
        return NormalizeConfig(config);
    }

    /// <inheritdoc />
    /// <summary>
    /// 將頻道設定正規化後儲存至持久化儲存層，並回傳正規化後的結果。
    /// 儲存前會先執行正規化，確保持久化資料格式一致。
    /// </summary>
    /// <param name="config">要儲存的頻道設定物件（可包含未正規化的資料）。</param>
    /// <param name="cancellationToken">非同步取消權杖。</param>
    /// <returns>正規化且已持久化的 <see cref="ChannelConfig"/>。</returns>
    public async Task<ChannelConfig> SaveAsync(ChannelConfig config, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeConfig(config);
        return await store.SaveAsync(normalized, cancellationToken);
    }

    /// <inheritdoc />
    /// <summary>
    /// 取得所有支援的頻道類型定義清單，包含每個頻道的欄位規格。
    /// </summary>
    /// <returns>包含 Line、Telegram、Email 等頻道類型定義的唯讀清單。</returns>
    public IReadOnlyList<ChannelTypeDefinition> GetChannelTypes() => Definitions;

    /// <inheritdoc />
    /// <summary>取得頻道設定 JSON 檔案的完整路徑。</summary>
    /// <returns>設定檔的絕對路徑字串。</returns>
    public string GetSettingsFilePath() => store.GetFilePath();

    /// <summary>
    /// 對整個 <see cref="ChannelConfig"/> 進行正規化處理：
    /// <list type="number">
    ///   <item>確保 <see cref="ChannelConfig.Channels"/> 字典不為 null（初始化為不區分大小寫的空字典）</item>
    ///   <item>過濾掉鍵值為空白的無效頻道項目</item>
    ///   <item>去除所有頻道鍵名的前後空白</item>
    ///   <item>對每個頻道的 <see cref="ChannelSettings"/> 進行個別正規化（見 <see cref="NormalizeSettings"/>）</item>
    ///   <item>重建字典以確保鍵值比較器為不區分大小寫</item>
    /// </list>
    /// </summary>
    /// <param name="config">要正規化的頻道設定物件。</param>
    /// <returns>正規化後的 <see cref="ChannelConfig"/>（與傳入物件相同參考，但內容已修改）。</returns>
    private static ChannelConfig NormalizeConfig(ChannelConfig config)
    {
        // 若 Channels 為 null（例如反序列化失敗），初始化為不區分大小寫的空字典
        config.Channels ??= new Dictionary<string, ChannelSettings>(StringComparer.OrdinalIgnoreCase);

        // 重建字典：過濾空鍵、去除空白、逐一正規化每個頻道設定
        // 最終使用不區分大小寫的比較器確保查詢一致性
        config.Channels = config.Channels
            .Where(x => !string.IsNullOrWhiteSpace(x.Key))
            .ToDictionary(
                x => x.Key.Trim(),
                x => NormalizeSettings(x.Key.Trim(), x.Value),
                StringComparer.OrdinalIgnoreCase);

        return config;
    }

    /// <summary>
    /// 對單一頻道的 <see cref="ChannelSettings"/> 進行正規化處理：
    /// <list type="number">
    ///   <item>過濾掉鍵值或值任一為空白的無效參數項目</item>
    ///   <item>去除所有參數鍵名與值的前後空白</item>
    ///   <item>重建不區分大小寫的參數字典</item>
    ///   <item>依頻道類型進行參數鍵名重命名（例如舊版 "Token" 重命名為 "ChannelAccessToken"）</item>
    /// </list>
    /// </summary>
    /// <param name="channelName">頻道識別碼，用於判斷應套用哪種重命名規則。</param>
    /// <param name="settings">要正規化的頻道設定物件。</param>
    /// <returns>正規化後的新 <see cref="ChannelSettings"/> 實例。</returns>
    private static ChannelSettings NormalizeSettings(string channelName, ChannelSettings settings)
    {
        // 第一步：過濾空值並去除前後空白，重建不區分大小寫的參數字典
        var parameters = settings.Parameters
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && !string.IsNullOrWhiteSpace(pair.Value))
            .ToDictionary(pair => pair.Key.Trim(), pair => pair.Value.Trim(), StringComparer.OrdinalIgnoreCase);

        // 第二步：依頻道類型套用參數鍵名重命名規則，處理舊版/縮短版鍵名的相容性
        if (channelName.Equals("Line", StringComparison.OrdinalIgnoreCase))
        {
            // Line 頻道：將舊版的 "Token" 重命名為標準鍵名 "ChannelAccessToken"
            Rename(parameters, "Token", "ChannelAccessToken");
            // Line 頻道：將舊版的 "Secret" 重命名為標準鍵名 "ChannelSecret"
            Rename(parameters, "Secret", "ChannelSecret");
        }
        else if (channelName.Equals("Telegram", StringComparison.OrdinalIgnoreCase))
        {
            // Telegram 頻道：將舊版的 "Token" 重命名為標準鍵名 "BotToken"
            Rename(parameters, "Token", "BotToken");
        }

        return new ChannelSettings
        {
            Enabled = settings.Enabled,
            Parameters = parameters
        };
    }

    /// <summary>
    /// 在參數字典中將 <paramref name="oldKey"/> 重命名為 <paramref name="newKey"/>。
    /// 只有在 <paramref name="newKey"/> 不存在且 <paramref name="oldKey"/> 存在的情況下才進行重命名，
    /// 避免覆蓋已有正確鍵名的值。
    /// <para>
    /// 無論重命名是否發生，最後都會移除 <paramref name="oldKey"/>，
    /// 確保字典中不存在舊版鍵名。
    /// </para>
    /// </summary>
    /// <param name="parameters">要進行重命名操作的參數字典（原地修改）。</param>
    /// <param name="oldKey">要被替換的舊版鍵名。</param>
    /// <param name="newKey">替換後的標準鍵名。</param>
    private static void Rename(IDictionary<string, string> parameters, string oldKey, string newKey)
    {
        // 僅在新鍵不存在時才將舊鍵的值移入新鍵，防止覆蓋使用者已用標準鍵名填寫的值
        if (!parameters.ContainsKey(newKey) && parameters.TryGetValue(oldKey, out var value))
        {
            parameters[newKey] = value;
        }

        // 無論是否已遷移值，都移除舊鍵，確保字典中不殘留舊版鍵名
        parameters.Remove(oldKey);
    }

}
