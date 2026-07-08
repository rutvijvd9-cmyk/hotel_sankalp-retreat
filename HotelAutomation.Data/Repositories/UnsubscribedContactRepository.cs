namespace HotelAutomation.Data.Repositories;

using Dapper;
using HotelAutomation.Core.Models;

public sealed class UnsubscribedContactRepository
{
    private readonly SqliteConnectionFactory _factory;

    public UnsubscribedContactRepository(SqliteConnectionFactory factory) => _factory = factory;

    public async Task AddAsync(string phoneNumber, string source, CancellationToken ct = default)
    {
        await using var conn = await _factory.OpenAsync(ct);
        var sql = @"
            INSERT INTO UnsubscribedContacts (PhoneNumber, UnsubscribedAt, Source)
            VALUES (@PhoneNumber, @UnsubscribedAt, @Source)
            ON CONFLICT(PhoneNumber) DO NOTHING;";
        
        await conn.ExecuteAsync(sql, new
        {
            PhoneNumber = phoneNumber,
            UnsubscribedAt = DateTimeOffset.UtcNow.ToString("O"),
            Source = source
        });
    }

    public async Task RemoveAsync(string phoneNumber, CancellationToken ct = default)
    {
        await using var conn = await _factory.OpenAsync(ct);
        await conn.ExecuteAsync("DELETE FROM UnsubscribedContacts WHERE PhoneNumber = @PhoneNumber", new { PhoneNumber = phoneNumber });
    }

    public async Task<bool> IsUnsubscribedAsync(string phoneNumber, CancellationToken ct = default)
    {
        await using var conn = await _factory.OpenAsync(ct);
        var count = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM UnsubscribedContacts WHERE PhoneNumber = @PhoneNumber", 
            new { PhoneNumber = phoneNumber });
        
        return count > 0;
    }
}
