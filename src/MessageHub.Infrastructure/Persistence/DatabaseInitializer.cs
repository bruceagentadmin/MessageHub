using Microsoft.Data.Sqlite;
using Dapper;

namespace MessageHub.Infrastructure.Persistence;

/// <summary>
/// 資料庫初始化器 — 啟動時自動建立 SQLite 資料表。
/// </summary>
public static class DatabaseInitializer
{
    public static async Task InitializeAsync(string connectionString)
    {
        using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        await connection.ExecuteAsync("""
            CREATE TABLE IF NOT EXISTS message_logs (
                Id TEXT PRIMARY KEY,
                Timestamp TEXT NOT NULL,
                TenantId TEXT NOT NULL,
                Channel TEXT NOT NULL,
                Direction INTEGER NOT NULL,
                Status INTEGER NOT NULL,
                TargetId TEXT NOT NULL,
                TargetDisplayName TEXT,
                Content TEXT NOT NULL,
                Source TEXT NOT NULL,
                Details TEXT,
                CreatedAt TEXT NOT NULL DEFAULT (datetime('now'))
            );

            CREATE INDEX IF NOT EXISTS idx_message_logs_timestamp ON message_logs(Timestamp DESC);
            CREATE INDEX IF NOT EXISTS idx_message_logs_channel ON message_logs(Channel);
            CREATE INDEX IF NOT EXISTS idx_message_logs_target ON message_logs(TargetId);
            CREATE INDEX IF NOT EXISTS idx_message_logs_direction ON message_logs(Direction);
            CREATE INDEX IF NOT EXISTS idx_message_logs_status ON message_logs(Status);
            """);

        await connection.ExecuteAsync("""
            CREATE TABLE IF NOT EXISTS recent_targets (
                Channel TEXT PRIMARY KEY,
                TargetId TEXT NOT NULL,
                DisplayName TEXT,
                UpdatedAt TEXT NOT NULL
            );
            """);

        await connection.ExecuteAsync("""
            CREATE TABLE IF NOT EXISTS contacts (
                Id TEXT PRIMARY KEY,
                Channel TEXT NOT NULL,
                PlatformUserId TEXT NOT NULL,
                DisplayName TEXT,
                ChatId TEXT,
                FirstSeenAt TEXT NOT NULL,
                LastSeenAt TEXT NOT NULL,
                MessageCount INTEGER NOT NULL DEFAULT 0,
                UNIQUE(Channel, PlatformUserId)
            );

            CREATE INDEX IF NOT EXISTS idx_contacts_channel ON contacts(Channel);
            CREATE INDEX IF NOT EXISTS idx_contacts_last_seen ON contacts(LastSeenAt DESC);
            """);
    }
}
