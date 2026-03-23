using MessageHub.Domain.Models;
using MessageHub.Domain.Repositories;

namespace MessageHub.Domain.Services;

/// <summary>
/// 聯絡人 Service 實作 — 委派 Repository 進行 CRUD。
/// </summary>
public sealed class ContactService(IContactRepository repository) : IContactService
{
    public Task<IReadOnlyList<Contact>> GetAllContactsAsync(CancellationToken ct = default)
        => repository.GetAllAsync(ct);

    public Task<IReadOnlyList<Contact>> GetContactsByChannelAsync(string channel, CancellationToken ct = default)
        => repository.GetByChannelAsync(channel, ct);

    public Task<Contact?> FindContactAsync(string channel, string platformUserId, CancellationToken ct = default)
        => repository.FindAsync(channel, platformUserId, ct);
}
