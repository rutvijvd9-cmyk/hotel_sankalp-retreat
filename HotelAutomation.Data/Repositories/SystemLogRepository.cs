namespace HotelAutomation.Data.Repositories;

using Dapper;
using HotelAutomation.Core.Models;

public sealed class SystemLogRepository
{
    private readonly SqliteConnectionFactory _factory;

    public SystemLogRepository(SqliteConnectionFactory factory) => _factory = factory;

    public async Task<int> LogAsync(
        string level,
        string category,
        string source,
        string message,
        string? details = null,
        string? exception = null,
        CancellationToken ct = default)
    {
        try
        {
            await using var conn = await _factory.OpenAsync(ct);
            return await conn.ExecuteAsync("""
                INSERT INTO SystemLogs (Timestamp, LogLevel, Category, Source, Message, Details, Exception)
                VALUES (@Timestamp, @LogLevel, @Category, @Source, @Message, @Details, @Exception);
                """, new
            {
                Timestamp = DateTimeOffset.UtcNow.ToString("O"),
                LogLevel = (level ?? "INFO").ToUpperInvariant(),
                Category = category ?? "General",
                Source = source ?? "",
                Message = message ?? "",
                Details = details,
                Exception = exception
            });
        }
        catch
        {
            // Failsafe: logging to DB should never crash the caller
            return 0;
        }
    }

    public async Task<IEnumerable<SystemLogEntry>> GetLogsAsync(
        string? category = null,
        string? level = null,
        string? search = null,
        int limit = 100,
        int offset = 0,
        CancellationToken ct = default)
    {
        await using var conn = await _factory.OpenAsync(ct);

        var sql = "SELECT * FROM SystemLogs WHERE 1=1";
        var dp = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(category) && !category.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            sql += " AND Category = @Category";
            dp.Add("Category", category);
        }

        if (!string.IsNullOrWhiteSpace(level) && !level.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            sql += " AND LogLevel = @LogLevel";
            dp.Add("LogLevel", level.ToUpperInvariant());
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            sql += " AND (Message LIKE @Search OR Source LIKE @Search OR Details LIKE @Search OR Exception LIKE @Search)";
            dp.Add("Search", $"%{search}%");
        }

        sql += " ORDER BY Timestamp DESC LIMIT @Limit OFFSET @Offset";
        dp.Add("Limit", Math.Clamp(limit, 1, 1000));
        dp.Add("Offset", Math.Max(0, offset));

        return await conn.QueryAsync<SystemLogEntry>(sql, dp);
    }

    public async Task<dynamic> GetStatsAsync(CancellationToken ct = default)
    {
        await using var conn = await _factory.OpenAsync(ct);

        var total = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM SystemLogs;");
        var fatal = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM SystemLogs WHERE LogLevel = 'FATAL';");
        var error = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM SystemLogs WHERE LogLevel = 'ERROR';");
        var warn = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM SystemLogs WHERE LogLevel = 'WARN';");
        var info = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM SystemLogs WHERE LogLevel = 'INFO';");

        var categoryBreakdown = await conn.QueryAsync<dynamic>(@"
            SELECT Category, COUNT(*) as Count
            FROM SystemLogs
            GROUP BY Category
            ORDER BY Count DESC;");

        return new
        {
            Total = total,
            FatalCount = fatal,
            ErrorCount = error,
            WarnCount = warn,
            InfoCount = info,
            Categories = categoryBreakdown
        };
    }

    public async Task<int> ClearLogsAsync(int olderThanDays = 30, CancellationToken ct = default)
    {
        await using var conn = await _factory.OpenAsync(ct);
        var cutoff = DateTimeOffset.UtcNow.AddDays(-olderThanDays).ToString("O");
        return await conn.ExecuteAsync("DELETE FROM SystemLogs WHERE Timestamp < @Cutoff;", new { Cutoff = cutoff });
    }
}
