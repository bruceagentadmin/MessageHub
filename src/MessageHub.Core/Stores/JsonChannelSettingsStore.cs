using System.Text.Json;
using MessageHub.Core.Models;

namespace MessageHub.Core.Stores;

/// <summary>
/// 以 JSON 檔案為後端的頻道設定持久化儲存實作。
/// 設定檔存放於應用程式根目錄上層五層的 <c>data/channel-settings.json</c>，
/// 確保在多環境（開發、容器、IIS）下路徑一致。
/// 支援新版格式（<see cref="ChannelConfig"/>）與舊版格式（<see cref="LegacyChannelConfig"/>）的反序列化回退。
/// </summary>
internal sealed class JsonChannelSettingsStore : IChannelSettingsStore
{
    /// <summary>
    /// JSON 序列化選項：縮排輸出以提升人工閱讀性，並保留原始屬性命名（不轉換為 camelCase）。
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = null
    };

    /// <summary>設定檔的完整檔案路徑。</summary>
    private readonly string _filePath;

    /// <summary>
    /// 初始化 <see cref="JsonChannelSettingsStore"/>，計算並建立設定檔所在目錄。
    /// 目錄路徑由 <see cref="AppContext.BaseDirectory"/> 向上追溯五層後進入 <c>data/</c> 資料夾。
    /// </summary>
    public JsonChannelSettingsStore()
    {
        var baseDirectory = AppContext.BaseDirectory;
        // 由 bin/Debug/netX.X/ 向上追溯到儲存庫根目錄的 data/ 資料夾
        var dataDirectory = Path.GetFullPath(Path.Combine(baseDirectory, "..", "..", "..", "..", "..", "data"));
        // 若目錄不存在則自動建立，避免第一次啟動時寫入失敗
        Directory.CreateDirectory(dataDirectory);
        _filePath = Path.Combine(dataDirectory, "channel-settings.json");
    }

    /// <inheritdoc />
    /// <summary>
    /// 從 JSON 檔案讀取頻道設定。
    /// 若檔案不存在，則建立預設設定並回傳；
    /// 若 JSON 內容為空，回傳空白的 <see cref="ChannelConfig"/>；
    /// 優先以新版格式反序列化，失敗則嘗試舊版格式，兩者均失敗則回傳空白設定。
    /// </summary>
    /// <param name="cancellationToken">非同步取消權杖。</param>
    /// <returns>載入並解析後的 <see cref="ChannelConfig"/>。</returns>
    public async Task<ChannelConfig> LoadAsync(CancellationToken cancellationToken = default)
    {
        // 設定檔不存在：建立含預設頻道的初始設定並寫入磁碟
        if (!File.Exists(_filePath))
        {
            var defaultConfig = CreateDefault();
            await SaveAsync(defaultConfig, cancellationToken);
            return defaultConfig;
        }

        var json = await File.ReadAllTextAsync(_filePath, cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
        {
            // 檔案存在但內容空白，視為未設定狀態
            return new ChannelConfig();
        }

        // 先嘗試新版格式；若格式不符（JsonException）再回退至舊版格式
        var config = TryDeserializeCurrent(json) ?? TryDeserializeLegacy(json);
        return config ?? new ChannelConfig();
    }

    /// <inheritdoc />
    /// <summary>
    /// 將頻道設定序列化為 JSON 並原子性地寫入設定檔。
    /// </summary>
    /// <param name="config">要儲存的頻道設定物件。</param>
    /// <param name="cancellationToken">非同步取消權杖。</param>
    /// <returns>已儲存的 <see cref="ChannelConfig"/>（與傳入值相同）。</returns>
    public async Task<ChannelConfig> SaveAsync(ChannelConfig config, CancellationToken cancellationToken = default)
    {
        // File.Create 以截斷模式開啟，確保舊內容被覆蓋而不是追加
        await using var stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, config, JsonOptions, cancellationToken);
        return config;
    }

    /// <inheritdoc />
    /// <summary>取得設定檔的完整檔案系統路徑，供上層服務用於顯示或除錯。</summary>
    /// <returns>設定檔的絕對路徑字串。</returns>
    public string GetFilePath() => _filePath;

    /// <summary>
    /// 嘗試以新版 <see cref="ChannelConfig"/> 格式反序列化 JSON 字串。
    /// 若 JSON 結構不符，捕捉 <see cref="JsonException"/> 並回傳 <c>null</c>。
    /// </summary>
    /// <param name="json">要解析的 JSON 字串。</param>
    /// <returns>反序列化成功時回傳 <see cref="ChannelConfig"/>；否則回傳 <c>null</c>。</returns>
    private static ChannelConfig? TryDeserializeCurrent(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<ChannelConfig>(json, JsonOptions);
        }
        catch (JsonException)
        {
            // JSON 格式不符新版結構，靜默返回 null 以進行舊版回退
            return null;
        }
    }

    /// <summary>
    /// 嘗試以舊版 <see cref="LegacyChannelConfig"/> 格式反序列化，並轉換為新版 <see cref="ChannelConfig"/>。
    /// 舊版格式使用陣列形式的頻道清單，每個項目含 <c>Id</c>、<c>Type</c>、<c>Config</c> 屬性。
    /// </summary>
    /// <param name="json">要解析的 JSON 字串。</param>
    /// <returns>轉換成功時回傳新版 <see cref="ChannelConfig"/>；否則回傳 <c>null</c>。</returns>
    private static ChannelConfig? TryDeserializeLegacy(string json)
    {
        try
        {
            var legacy = JsonSerializer.Deserialize<LegacyChannelConfig>(json, JsonOptions);
            if (legacy?.Channels is null)
            {
                return null;
            }

            var result = new ChannelConfig();
            foreach (var item in legacy.Channels)
            {
                // 跳過 Id 為空白的無效項目
                if (string.IsNullOrWhiteSpace(item.Id))
                {
                    continue;
                }

                // 將舊版的 Config 字典轉換為新版的 Parameters，並以不區分大小寫的鍵值比較器建立新字典
                result.Channels[item.Id.Trim()] = new ChannelSettings
                {
                    Enabled = item.Enabled,
                    Parameters = new Dictionary<string, string>(item.Config ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase)
                };
            }

            return result;
        }
        catch (JsonException)
        {
            // 舊版格式也解析失敗，回傳 null 讓呼叫端使用空白設定
            return null;
        }
    }

    /// <summary>
    /// 建立含預設頻道設定的初始 <see cref="ChannelConfig"/>。
    /// 預設包含 Line 與 Telegram 兩個頻道，Token 等敏感參數留空供使用者填寫，
    /// WebhookUrl 預設指向本機開發隧道（devtunnel）。
    /// </summary>
    /// <returns>包含 Line 與 Telegram 預設設定的 <see cref="ChannelConfig"/>。</returns>
    private static ChannelConfig CreateDefault() => new()
    {
        Channels =
        new Dictionary<string, ChannelSettings>(StringComparer.OrdinalIgnoreCase)
        {
            ["line"] = new ChannelSettings
            {
                Enabled = true,
                Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["ChannelAccessToken"] = "",
                    ["ChannelSecret"] = "",
                    ["WebhookUrl"] = "https://3vmcf3ql-5001.jpe1.devtunnels.ms/api/line/webhook",
                    ["WebhookMode"] = "devtunnel"
                }
            },
            ["telegram"] = new ChannelSettings
            {
                Enabled = true,
                Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["BotToken"] = "",
                    ["WebhookUrl"] = "https://3vmcf3ql-5001.jpe1.devtunnels.ms/api/telegram/webhook",
                    ["WebhookMode"] = "devtunnel"
                }
            }
        }
    };

    /// <summary>
    /// 舊版設定檔的根物件格式，對應陣列式頻道清單結構（已廢棄，僅用於向下相容讀取）。
    /// </summary>
    private sealed class LegacyChannelConfig
    {
        /// <summary>舊版頻道設定項目清單。</summary>
        public List<LegacyChannelItem> Channels { get; set; } = [];
    }

    /// <summary>
    /// 舊版頻道設定項目，對應舊格式 JSON 中每個頻道物件的屬性（已廢棄，僅用於向下相容讀取）。
    /// </summary>
    private sealed class LegacyChannelItem
    {
        /// <summary>頻道識別碼（例如 "line"、"telegram"）。</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>頻道類型描述（舊版欄位，新版已由 Id 隱含判斷）。</summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>頻道是否啟用。</summary>
        public bool Enabled { get; set; }

        /// <summary>頻道設定參數字典（舊版稱為 Config，新版改稱 Parameters）。</summary>
        public Dictionary<string, string> Config { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
