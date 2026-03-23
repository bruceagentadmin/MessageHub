using MessageHub.Domain.Models;

namespace MessageHub.Domain.Services;

/// <summary>
/// 聯絡人 Service 介面。
/// </summary>
public interface IContactService
{
    Task<IReadOnlyList<Contact>> GetAllContactsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Contact>> GetContactsByChannelAsync(string channel, CancellationToken ct = default);
    Task<Contact?> FindContactAsync(string channel, string platformUserId, CancellationToken ct = default);
}
