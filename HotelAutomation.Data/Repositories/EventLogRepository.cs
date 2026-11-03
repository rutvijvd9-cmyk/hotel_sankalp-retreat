namespace HotelAutomation.Data.Repositories;

using Dapper;
using HotelAutomation.Core.Models;

public sealed class EventLogRepository
{
    private readonly SqliteConnectionFactory _factory;

    public EventLogRepository(SqliteConnectionFactory factory) => _factory = factory;

    public async Task LogAsync(PmsEvent pmsEvent, int? outboxMessageId = null, CancellationToken ct = default)
    {
        await using var conn = await _factory.OpenAsync(ct);
        await conn.ExecuteAsync("""
            INSERT INTO EventLog (EventType, RawPayloadJson, OutboxMessageId, OccurredAt)
            VALUES (@EventType, @RawPayloadJson, @OutboxMessageId, @OccurredAt)
            """, new
        {
            pmsEvent.EventType,
            RawPayloadJson = System.Text.Json.JsonSerializer.Serialize(pmsEvent),
            OutboxMessageId = outboxMessageId,
            OccurredAt = pmsEvent.Timestamp.ToString("O")
        });
    }

    public async Task<IEnumerable<dynamic>> GetRecentLogsAsync(int limit = 100, CancellationToken ct = default)
    {
        await using var conn = await _factory.OpenAsync(ct);
        return await conn.QueryAsync("SELECT * FROM EventLog ORDER BY OccurredAt DESC LIMIT @Limit", new { Limit = limit });
    }

    public async Task<int> ClearOldLogsAsync(int olderThanDays = 15, CancellationToken ct = default)
    {
        await using var conn = await _factory.OpenAsync(ct);
        var cutoff = DateTimeOffset.UtcNow.AddDays(-olderThanDays).ToString("O");
        return await conn.ExecuteAsync("DELETE FROM EventLog WHERE OccurredAt < @Cutoff;", new { Cutoff = cutoff });
    }
}
