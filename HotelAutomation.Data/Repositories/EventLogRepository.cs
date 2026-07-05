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
}
