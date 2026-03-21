namespace MessageHub.Core;

/// <summary>
/// 參數提供者 — 負責依鍵值異步獲取多租戶環境下的配置參數。
/// 對應規格文件中的 ICommonParameterProvider。
/// </summary>
public interface ICommonParameterProvider
{
    /// <summary>泛型方法，用於讀取特定的配置物件 (如 ChannelConfig)</summary>
    Task<T?> GetParameterByKeyAsync<T>(string key, CancellationToken cancellationToken = default) where T : class;
}
