namespace HotelAutomation.Data.Repositories;

using Dapper;
using HotelAutomation.Core.Interfaces;
using HotelAutomation.Core.Models;

public sealed class OutboxRepository : IOutboxRepository
{
    private readonly SqliteConnectionFactory _factory;

    public OutboxRepository(SqliteConnectionFactory factory) => _factory = factory;

    public async Task<int> AddAsync(OutboxMessage message, CancellationToken ct = default)
    {
        await using var conn = await _factory.OpenAsync(ct);
        return await conn.ExecuteScalarAsync<int>("""
            INSERT INTO OutboxMessages (ToPhoneNumber, TemplateId, ParametersJson, Status, RetryCount, CreatedAt)
            VALUES (@ToPhoneNumber, @TemplateId, @ParametersJson, @Status, @RetryCount, @CreatedAt);
            SELECT last_insert_rowid();
            """, new
        {
            message.ToPhoneNumber,
            message.TemplateId,
            message.ParametersJson,
            message.Status,
            message.RetryCount,
            CreatedAt = message.CreatedAt.ToString("O")
        });
    }

    public async Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(int limit, CancellationToken ct = default)
    {
        await using var conn = await _factory.OpenAsync(ct);
        var results = await conn.QueryAsync<OutboxMessage>(
            "SELECT * FROM OutboxMessages WHERE Status = 'Pending' ORDER BY CreatedAt LIMIT @limit",
            new { limit });
        return results.AsList();
    }

    public async Task UpdateStatusAsync(int id, string status, int retryCount,
        DateTimeOffset? sentAt, CancellationToken ct = default)
    {
        await using var conn = await _factory.OpenAsync(ct);
        await conn.ExecuteAsync("""
            UPDATE OutboxMessages
            SET Status = @status, RetryCount = @retryCount, SentAt = @sentAt
            WHERE Id = @id
            """, new { id, status, retryCount, sentAt = sentAt?.ToString("O") });
    }
}
