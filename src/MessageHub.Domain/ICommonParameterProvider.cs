namespace MessageHub.Domain;

/// <summary>
/// 通用參數提供者介面 — 負責依鍵值非同步取得多租戶環境下的組態參數。
/// 對應規格文件中的 ICommonParameterProvider。
/// </summary>
public interface ICommonParameterProvider
{
    /// <summary>
    /// 非同步依鍵值讀取指定型別的組態參數物件。
    /// </summary>
    /// <typeparam name="T">
    /// 要讀取的組態物件型別（如 <c>ChannelConfig</c>），必須為參考型別。
    /// </typeparam>
    /// <param name="key">組態參數的鍵值識別碼（如頻道名稱或租戶識別碼）。</param>
    /// <param name="cancellationToken">用於取消非同步操作的權杖。</param>
    /// <returns>
    /// 若找到對應的組態物件，回傳型別為 <typeparamref name="T"/> 的實例；
    /// 否則回傳 <see langword="null"/>。
    /// </returns>
    Task<T?> GetParameterByKeyAsync<T>(string key, CancellationToken cancellationToken = default) where T : class;
}
