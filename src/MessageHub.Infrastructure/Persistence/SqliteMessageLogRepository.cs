using Dapper;
using MessageHub.Core;
using MessageHub.Core.Models;
using MessageHub.Domain.Models;
using MessageHub.Domain.Repositories;

namespace MessageHub.Infrastructure.Persistence;

/// <summary>
/// SQLite 訊息日誌 Repository — 同時實作：
/// - IMessageLogStore (Core)：供 MessageCoordinator、ChannelManager 寫入/讀取最近日誌
/// - IMessageLogRepository (Domain)：供 HistoryService 進階查詢
/// </summary>
public sealed class SqliteMessageLogRepository(SqliteConnectionFactory factory)
    : IMessageLogStore, IMessageLogRepository
{
    // ── IMessageLogStore 實作 ──

    public async Task AddAsync(MessageLogEntry entry, CancellationToken ct = default)
    {
        using var conn = factory.CreateConnection();
        await conn.ExecuteAsync("""
            INSERT INTO message_logs (Id, Timestamp, TenantId, Channel, Direction, Status,
                                      TargetId, TargetDisplayName, Content, Source, Details)
            VALUES (@Id, @Timestamp, @TenantId, @Channel, @Direction, @Status,
                    @TargetId, @TargetDisplayName, @Content, @Source, @Details)
            """, new
        {
            Id = entry.Id.ToString(),
            Timestamp = entry.Timestamp.ToString("o"),
            entry.TenantId,
            entry.Channel,
            Direction = (int)entry.Direction,
            Status = (int)entry.Status,
            entry.TargetId,
            entry.TargetDisplayName,
            entry.Content,
            entry.Source,
            entry.Details
        });
    }

    public async Task<IReadOnlyList<MessageLogEntry>> GetRecentAsync(int count, CancellationToken ct = default)
    {
        var clamped = Math.Clamp(count, 1, 200);
        using var conn = factory.CreateConnection();
        var rows = await conn.QueryAsync<MessageLogRow>(
            "SELECT * FROM message_logs ORDER BY Timestamp DESC LIMIT @count",
            new { count = clamped });

        return rows.Select(r => new MessageLogEntry(
            Guid.Parse(r.Id), DateTimeOffset.Parse(r.Timestamp), r.TenantId, r.Channel,
            (MessageDirection)r.Direction, (DeliveryStatus)r.Status,
            r.TargetId, r.TargetDisplayName, r.Content, r.Source, r.Details
        )).ToArray();
    }

    // ── IMessageLogRepository 實作 ──

    async Task IMessageLogRepository.AddAsync(MessageLogRecord record, CancellationToken ct)
    {
        using var conn = factory.CreateConnection();
        await conn.ExecuteAsync("""
            INSERT INTO message_logs (Id, Timestamp, TenantId, Channel, Direction, Status,
                                      TargetId, TargetDisplayName, Content, Source, Details)
            VALUES (@Id, @Timestamp, @TenantId, @Channel, @Direction, @Status,
                    @TargetId, @TargetDisplayName, @Content, @Source, @Details)
            """, new
        {
            Id = record.Id.ToString(),
            Timestamp = record.Timestamp.ToString("o"),
            record.TenantId,
            record.Channel,
            record.Direction,
            record.Status,
            record.TargetId,
            record.TargetDisplayName,
            record.Content,
            record.Source,
            record.Details
        });
    }

    async Task<IReadOnlyList<MessageLogRecord>> IMessageLogRepository.GetRecentAsync(int count, CancellationToken ct)
    {
        var clamped = Math.Clamp(count, 1, 200);
        using var conn = factory.CreateConnection();
        var rows = await conn.QueryAsync<MessageLogRow>(
            "SELECT * FROM message_logs ORDER BY Timestamp DESC LIMIT @count",
            new { count = clamped });

        return rows.Select(r => new MessageLogRecord(
            Guid.Parse(r.Id), DateTimeOffset.Parse(r.Timestamp), r.TenantId, r.Channel,
            r.Direction, r.Status, r.TargetId, r.TargetDisplayName, r.Content, r.Source, r.Details
        )).ToArray();
    }

    public async Task<PagedResult<MessageLogRecord>> QueryAsync(MessageLogQuery query, CancellationToken ct = default)
    {
        using var conn = factory.CreateConnection();

        var where = "WHERE 1=1";
        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(query.Channel))
        {
            where += " AND Channel = @Channel";
            parameters.Add("Channel", query.Channel);
        }
        if (query.Direction.HasValue)
        {
            where += " AND Direction = @Direction";
            parameters.Add("Direction", query.Direction.Value);
        }
        if (query.Status.HasValue)
        {
            where += " AND Status = @Status";
            parameters.Add("Status", query.Status.Value);
        }
        if (!string.IsNullOrWhiteSpace(query.TargetId))
        {
            where += " AND TargetId = @TargetId";
            parameters.Add("TargetId", query.TargetId);
        }
        if (!string.IsNullOrWhiteSpace(query.SenderId))
        {
            where += " AND (TargetId = @SenderId OR TargetDisplayName = @SenderId)";
            parameters.Add("SenderId", query.SenderId);
        }
        if (!string.IsNullOrWhiteSpace(query.ContentKeyword))
        {
            where += " AND Content LIKE @ContentKeyword";
            parameters.Add("ContentKeyword", $"%{query.ContentKeyword}%");
        }
        if (query.From.HasValue)
        {
            where += " AND Timestamp >= @From";
            parameters.Add("From", query.From.Value.ToString("o"));
        }
        if (query.To.HasValue)
        {
            where += " AND Timestamp <= @To";
            parameters.Add("To", query.To.Value.ToString("o"));
        }

        var countSql = $"SELECT COUNT(*) FROM message_logs {where}";
        var totalCount = await conn.ExecuteScalarAsync<int>(countSql, parameters);

        var offset = (Math.Max(query.Page, 1) - 1) * query.PageSize;
        var dataSql = $"SELECT * FROM message_logs {where} ORDER BY Timestamp DESC LIMIT @PageSize OFFSET @Offset";
        parameters.Add("PageSize", query.PageSize);
        parameters.Add("Offset", offset);

        var rows = await conn.QueryAsync<MessageLogRow>(dataSql, parameters);
        var items = rows.Select(r => new MessageLogRecord(
            Guid.Parse(r.Id), DateTimeOffset.Parse(r.Timestamp), r.TenantId, r.Channel,
            r.Direction, r.Status, r.TargetId, r.TargetDisplayName, r.Content, r.Source, r.Details
        )).ToArray();

        return new PagedResult<MessageLogRecord>(items, totalCount, query.Page, query.PageSize);
    }

    /// <summary>Dapper 映射用的內部 DTO。</summary>
    private sealed class MessageLogRow
    {
        public string Id { get; set; } = "";
        public string Timestamp { get; set; } = "";
        public string TenantId { get; set; } = "";
        public string Channel { get; set; } = "";
        public int Direction { get; set; }
        public int Status { get; set; }
        public string TargetId { get; set; } = "";
        public string? TargetDisplayName { get; set; }
        public string Content { get; set; } = "";
        public string Source { get; set; } = "";
        public string? Details { get; set; }
    }
}
