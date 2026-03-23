using MessageHub.Domain.Models;

namespace MessageHub.Domain.Repositories;

/// <summary>
/// 聯絡人 Repository — 提供聯絡人資料的持久化存取。
/// </summary>
public interface IContactRepository
{
    /// <summary>新增或更新聯絡人（以 Channel + PlatformUserId 為 key 做 UPSERT）。</summary>
    Task UpsertAsync(Contact contact, CancellationToken ct = default);

    /// <summary>依頻道取得聯絡人清單。</summary>
    Task<IReadOnlyList<Contact>> GetByChannelAsync(string channel, CancellationToken ct = default);

    /// <summary>取得所有聯絡人。</summary>
    Task<IReadOnlyList<Contact>> GetAllAsync(CancellationToken ct = default);

    /// <summary>依 Channel + PlatformUserId 查詢單一聯絡人。</summary>
    Task<Contact?> FindAsync(string channel, string platformUserId, CancellationToken ct = default);
}
