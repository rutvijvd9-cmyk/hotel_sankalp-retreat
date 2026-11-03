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
            INSERT INTO OutboxMessages (ToPhoneNumber, TemplateId, VariantId, ParametersJson, Status, RetryCount, CreatedAt, RuleId)
            VALUES (@ToPhoneNumber, @TemplateId, @VariantId, @ParametersJson, @Status, @RetryCount, @CreatedAt, @RuleId);
            SELECT last_insert_rowid();
            """, new
        {
            message.ToPhoneNumber,
            message.TemplateId,
            message.VariantId,
            message.ParametersJson,
            message.Status,
            message.RetryCount,
            CreatedAt = message.CreatedAt.ToString("O"),
            message.RuleId
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
        DateTimeOffset? sentAt, string? externalMessageId, string? failureReason = null, CancellationToken ct = default)
    {
        await using var conn = await _factory.OpenAsync(ct);
        await conn.ExecuteAsync("""
            UPDATE OutboxMessages
            SET Status = @status, RetryCount = @retryCount, SentAt = @sentAt, ExternalMessageId = @externalMessageId, ErrorMessage = @failureReason
            WHERE Id = @id
            """, new { id, status, retryCount, sentAt = sentAt?.ToString("O"), externalMessageId, failureReason });
    }

    public async Task<int> ClearOldMessagesAsync(int olderThanDays = 90, CancellationToken ct = default)
    {
        await using var conn = await _factory.OpenAsync(ct);
        var cutoff = DateTimeOffset.UtcNow.AddDays(-olderThanDays).ToString("O");
        return await conn.ExecuteAsync(
            "DELETE FROM OutboxMessages WHERE Status IN ('Sent', 'Delivered', 'Read', 'Failed') AND CreatedAt < @Cutoff",
            new { Cutoff = cutoff });
    }
}