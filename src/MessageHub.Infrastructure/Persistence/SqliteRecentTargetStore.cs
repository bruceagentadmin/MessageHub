using Dapper;
using MessageHub.Core;
using MessageHub.Core.Models;

namespace MessageHub.Infrastructure.Persistence;

/// <summary>
/// SQLite 最近互動目標 Store — 實作 IRecentTargetStore (Core)。
/// </summary>
public sealed class SqliteRecentTargetStore(SqliteConnectionFactory factory) : IRecentTargetStore
{
    public async Task SetLastTargetAsync(string channel, string targetId, string? displayName = null, CancellationToken ct = default)
    {
        using var conn = factory.CreateConnection();
        await conn.ExecuteAsync("""
            INSERT INTO recent_targets (Channel, TargetId, DisplayName, UpdatedAt)
            VALUES (@Channel, @TargetId, @DisplayName, @UpdatedAt)
            ON CONFLICT(Channel) DO UPDATE SET
                TargetId = @TargetId,
                DisplayName = @DisplayName,
                UpdatedAt = @UpdatedAt
            """, new
        {
            Channel = channel,
            TargetId = targetId,
            DisplayName = displayName,
            UpdatedAt = DateTimeOffset.UtcNow.ToString("o")
        });
    }

    public async Task<RecentTargetInfo?> GetLastTargetAsync(string channel, CancellationToken ct = default)
    {
        using var conn = factory.CreateConnection();
        var row = await conn.QueryFirstOrDefaultAsync<RecentTargetRow>(
            "SELECT * FROM recent_targets WHERE Channel = @Channel COLLATE NOCASE",
            new { Channel = channel });

        return row is null ? null : new RecentTargetInfo(row.Channel, row.TargetId, row.DisplayName, DateTimeOffset.Parse(row.UpdatedAt));
    }

    private sealed class RecentTargetRow
    {
        public string Channel { get; set; } = "";
        public string TargetId { get; set; } = "";
        public string? DisplayName { get; set; }
        public string UpdatedAt { get; set; } = "";
    }
}
