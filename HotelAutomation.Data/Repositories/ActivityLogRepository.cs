namespace HotelAutomation.Data.Repositories;

using Dapper;

public sealed class ActivityLogRepository
{
    private readonly SqliteConnectionFactory _factory;

    public ActivityLogRepository(SqliteConnectionFactory factory) => _factory = factory;

    /// <summary>Writes one activity entry to the ActivityLog table.</summary>
    public async Task LogAsync(
        string username,
        string action,
        string? details = null,
        string? ipAddress = null,
        CancellationToken ct = default)
    {
        await using var conn = await _factory.OpenAsync(ct);
        await conn.ExecuteAsync(
            "INSERT INTO ActivityLog (Username, Action, Details, IpAddress, OccurredAt) VALUES (@Username, @Action, @Details, @IpAddress, @OccurredAt)",
            new
            {
                Username   = username,
                Action     = action,
                Details    = details,
                IpAddress  = ipAddress,
                OccurredAt = DateTimeOffset.Now.ToString("O")
            });
    }

    /// <summary>Returns the most recent N activity log entries (all users).</summary>
    public async Task<IEnumerable<dynamic>> GetRecentLogsAsync(int limit = 200, CancellationToken ct = default)
    {
        await using var conn = await _factory.OpenAsync(ct);
        return await conn.QueryAsync(
            "SELECT Id, Username, Action, Details, IpAddress, OccurredAt FROM ActivityLog ORDER BY OccurredAt DESC LIMIT @limit",
            new { limit });
    }

    /// <summary>Returns activity log entries for a specific user.</summary>
    public async Task<IEnumerable<dynamic>> GetLogsByUserAsync(string username, int limit = 100, CancellationToken ct = default)
    {
        await using var conn = await _factory.OpenAsync(ct);
        return await conn.QueryAsync(
            "SELECT Id, Username, Action, Details, IpAddress, OccurredAt FROM ActivityLog WHERE Username = @username ORDER BY OccurredAt DESC LIMIT @limit",
            new { username, limit });
    }
}
