using MessageHub.Core;
using MessageHub.Domain.Repositories;
using MessageHub.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace MessageHub.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddMessageHubInfrastructure(this IServiceCollection services)
    {
        // ─── Polly 重試管線 ──────────────────────────────────────────────────
        services.AddSingleton<IRetryPipeline, PollyRetryPipeline>();

        // ─── SQLite 連線工廠 ─────────────────────────────────────────────────
        var dbPath = Path.Combine("data", "messagehub.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        var connectionString = $"Data Source={dbPath}";
        services.AddSingleton(new SqliteConnectionFactory(connectionString));

        // ─── 訊息日誌（同時實作 Core IMessageLogStore + Domain IMessageLogRepository）───
        services.AddSingleton<SqliteMessageLogRepository>();
        services.AddSingleton<IMessageLogStore>(sp => sp.GetRequiredService<SqliteMessageLogRepository>());
        services.AddSingleton<IMessageLogRepository>(sp => sp.GetRequiredService<SqliteMessageLogRepository>());

        // ─── 最近互動目標（Core IRecentTargetStore）─────────────────────────
        services.AddSingleton<IRecentTargetStore, SqliteRecentTargetStore>();

        // ─── 聯絡人（Domain IContactRepository）─────────────────────────────
        services.AddSingleton<IContactRepository, SqliteContactRepository>();

        return services;
    }

    /// <summary>
    /// 初始化 SQLite 資料庫 — 建立資料表與索引（應在 app.Build() 後、app.Run() 前呼叫）。
    /// </summary>
    public static async Task InitializeDatabaseAsync(this IServiceProvider services)
    {
        var factory = services.GetRequiredService<SqliteConnectionFactory>();
        await DatabaseInitializer.InitializeAsync(factory.ConnectionString);
    }
}
