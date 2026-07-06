namespace HotelAutomation.Data.Repositories;

using Dapper;
using HotelAutomation.Core.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public sealed class ContactRepository
{
    private readonly SqliteConnectionFactory _factory;

    public ContactRepository(SqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task UpsertAsync(Contact contact, CancellationToken ct = default)
    {
        await using var conn = await _factory.OpenAsync(ct);
        await conn.ExecuteAsync("""
            INSERT INTO Contacts (PhoneNumber, FirstName, LastName, CategoryId, CustomFieldsJson, CreatedAt)
            VALUES (@PhoneNumber, @FirstName, @LastName, @CategoryId, @CustomFieldsJson, @CreatedAt)
            ON CONFLICT(PhoneNumber) DO UPDATE SET
                FirstName = excluded.FirstName,
                LastName = excluded.LastName,
                CategoryId = excluded.CategoryId,
                CustomFieldsJson = excluded.CustomFieldsJson;
            """, new
        {
            contact.PhoneNumber,
            contact.FirstName,
            contact.LastName,
            contact.CategoryId,
            contact.CustomFieldsJson,
            CreatedAt = contact.CreatedAt.ToString("O")
        });
    }

    public async Task<IReadOnlyList<Contact>> GetAllAsync(int? categoryId = null, CancellationToken ct = default)
    {
        await using var conn = await _factory.OpenAsync(ct);
        
        if (categoryId.HasValue)
        {
            var results = await conn.QueryAsync<Contact>(
                "SELECT * FROM Contacts WHERE CategoryId = @categoryId ORDER BY CreatedAt DESC",
                new { categoryId });
            return results.AsList();
        }
        else
        {
            var results = await conn.QueryAsync<Contact>("SELECT * FROM Contacts ORDER BY CreatedAt DESC");
            return results.AsList();
        }
    }
}
