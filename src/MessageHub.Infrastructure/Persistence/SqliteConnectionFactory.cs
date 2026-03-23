using Microsoft.Data.Sqlite;

namespace MessageHub.Infrastructure.Persistence;

/// <summary>
/// SQLite 連線工廠 — 封裝連線字串，提供建立連線的統一入口。
/// </summary>
public sealed class SqliteConnectionFactory(string connectionString)
{
    public string ConnectionString => connectionString;

    public SqliteConnection CreateConnection() => new(connectionString);
}
