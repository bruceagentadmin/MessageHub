using Dapper;
using MessageHub.Domain.Models;
using MessageHub.Domain.Repositories;

namespace MessageHub.Infrastructure.Persistence;

/// <summary>
/// SQLite 聯絡人 Repository — 實作 IContactRepository (Domain)。
/// </summary>
public sealed class SqliteContactRepository(SqliteConnectionFactory factory) : IContactRepository
{
    public async Task UpsertAsync(Contact contact, CancellationToken ct = default)
    {
        using var conn = factory.CreateConnection();
        await conn.ExecuteAsync("""
            INSERT INTO contacts (Id, Channel, PlatformUserId, DisplayName, ChatId, FirstSeenAt, LastSeenAt, MessageCount)
            VALUES (@Id, @Channel, @PlatformUserId, @DisplayName, @ChatId, @FirstSeenAt, @LastSeenAt, @MessageCount)
            ON CONFLICT(Channel, PlatformUserId) DO UPDATE SET
                DisplayName = COALESCE(@DisplayName, contacts.DisplayName),
                ChatId = @ChatId,
                LastSeenAt = @LastSeenAt,
                MessageCount = @MessageCount
            """, new
        {
            Id = contact.Id.ToString(),
            contact.Channel,
            contact.PlatformUserId,
            contact.DisplayName,
            contact.ChatId,
            FirstSeenAt = contact.FirstSeenAt.ToString("o"),
            LastSeenAt = contact.LastSeenAt.ToString("o"),
            contact.MessageCount
        });
    }

    public async Task<IReadOnlyList<Contact>> GetByChannelAsync(string channel, CancellationToken ct = default)
    {
        using var conn = factory.CreateConnection();
        var rows = await conn.QueryAsync<ContactRow>(
            "SELECT * FROM contacts WHERE Channel = @Channel COLLATE NOCASE ORDER BY LastSeenAt DESC",
            new { Channel = channel });
        return rows.Select(MapToContact).ToArray();
    }

    public async Task<IReadOnlyList<Contact>> GetAllAsync(CancellationToken ct = default)
    {
        using var conn = factory.CreateConnection();
        var rows = await conn.QueryAsync<ContactRow>(
            "SELECT * FROM contacts ORDER BY LastSeenAt DESC");
        return rows.Select(MapToContact).ToArray();
    }

    public async Task<Contact?> FindAsync(string channel, string platformUserId, CancellationToken ct = default)
    {
        using var conn = factory.CreateConnection();
        var row = await conn.QueryFirstOrDefaultAsync<ContactRow>(
            "SELECT * FROM contacts WHERE Channel = @Channel COLLATE NOCASE AND PlatformUserId = @PlatformUserId",
            new { Channel = channel, PlatformUserId = platformUserId });
        return row is null ? null : MapToContact(row);
    }

    private static Contact MapToContact(ContactRow r) => new(
        Guid.Parse(r.Id), r.Channel, r.PlatformUserId, r.DisplayName, r.ChatId,
        DateTimeOffset.Parse(r.FirstSeenAt), DateTimeOffset.Parse(r.LastSeenAt), r.MessageCount);

    private sealed class ContactRow
    {
        public string Id { get; set; } = "";
        public string Channel { get; set; } = "";
        public string PlatformUserId { get; set; } = "";
        public string? DisplayName { get; set; }
        public string? ChatId { get; set; }
        public string FirstSeenAt { get; set; } = "";
        public string LastSeenAt { get; set; } = "";
        public int MessageCount { get; set; }
    }
}
