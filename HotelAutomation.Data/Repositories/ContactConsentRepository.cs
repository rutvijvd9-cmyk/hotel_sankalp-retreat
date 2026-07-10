namespace HotelAutomation.Data.Repositories;

using Dapper;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public sealed class ContactConsentRepository
{
    private readonly SqliteConnectionFactory _factory;

    public ContactConsentRepository(SqliteConnectionFactory factory) => _factory = factory;

    public async Task UpsertConsentAsync(int contactId, int categoryId, bool isSubscribed, CancellationToken ct = default)
    {
        await using var conn = await _factory.OpenAsync(ct);
        var sql = @"
            INSERT INTO ContactConsent (ContactId, CategoryId, IsSubscribed, UpdatedAt)
            VALUES (@ContactId, @CategoryId, @IsSubscribed, @UpdatedAt)
            ON CONFLICT(ContactId, CategoryId) DO UPDATE SET 
                IsSubscribed = excluded.IsSubscribed,
                UpdatedAt = excluded.UpdatedAt;";

        await conn.ExecuteAsync(sql, new
        {
            ContactId = contactId,
            CategoryId = categoryId,
            IsSubscribed = isSubscribed ? 1 : 0,
            UpdatedAt = DateTimeOffset.UtcNow.ToString("O")
        });
    }

    public async Task<bool> IsSubscribedAsync(int contactId, int categoryId, CancellationToken ct = default)
    {
        await using var conn = await _factory.OpenAsync(ct);
        var sql = "SELECT IsSubscribed FROM ContactConsent WHERE ContactId = @ContactId AND CategoryId = @CategoryId";
        var isSubscribed = await conn.ExecuteScalarAsync<int?>(sql, new { ContactId = contactId, CategoryId = categoryId });
        
        // Default to true if no record exists
        return isSubscribed == null || isSubscribed == 1;
    }

    public async Task<List<dynamic>> GetConsentPreferencesAsync(int contactId, CancellationToken ct = default)
    {
        await using var conn = await _factory.OpenAsync(ct);
        var sql = @"
            SELECT cat.Id as CategoryId, cat.Name as CategoryName, COALESCE(cc.IsSubscribed, 1) as IsSubscribed
            FROM ContactCategories cat
            LEFT JOIN ContactConsent cc ON cc.CategoryId = cat.Id AND cc.ContactId = @ContactId
        ";
        return (await conn.QueryAsync(sql, new { ContactId = contactId })).AsList();
    }
}
