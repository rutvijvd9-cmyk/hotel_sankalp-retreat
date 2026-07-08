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

    public async Task UpsertBulkAsync(IEnumerable<Contact> contacts, CancellationToken ct = default)
    {
        await using var conn = await _factory.OpenAsync(ct);
        using var transaction = conn.BeginTransaction();
        
        var sql = """
            INSERT INTO Contacts (PhoneNumber, FirstName, LastName, CategoryId, CustomFieldsJson, CreatedAt)
            VALUES (@PhoneNumber, @FirstName, @LastName, @CategoryId, @CustomFieldsJson, @CreatedAt)
            ON CONFLICT(PhoneNumber) DO UPDATE SET
                FirstName = excluded.FirstName,
                LastName = excluded.LastName,
                CategoryId = excluded.CategoryId,
                CustomFieldsJson = excluded.CustomFieldsJson;
            """;

        await conn.ExecuteAsync(sql, contacts.Select(c => new
        {
            c.PhoneNumber,
            c.FirstName,
            c.LastName,
            c.CategoryId,
            c.CustomFieldsJson,
            CreatedAt = c.CreatedAt.ToString("O")
        }), transaction: transaction);
        
        transaction.Commit();
    }

    public async Task<(IReadOnlyList<Contact> Contacts, int TotalCount)> GetAllAsync(int? categoryId = null, int page = 1, int limit = 50, CancellationToken ct = default)
    {
        await using var conn = await _factory.OpenAsync(ct);
        int offset = (page - 1) * limit;
        
        string baseQuery = categoryId.HasValue ? "FROM Contacts WHERE CategoryId = @categoryId" : "FROM Contacts";
        
        var totalCount = await conn.ExecuteScalarAsync<int>($"SELECT COUNT(*) {baseQuery}", new { categoryId });
        
        var results = await conn.QueryAsync<Contact>(
            $"SELECT * {baseQuery} ORDER BY CreatedAt DESC LIMIT @limit OFFSET @offset",
            new { categoryId, limit, offset });
            
        return (results.AsList(), totalCount);
    }

    public async Task<int> DeleteAllAsync(CancellationToken ct = default)
    {
        await using var conn = await _factory.OpenAsync(ct);
        return await conn.ExecuteAsync("DELETE FROM Contacts");
    }

    public async Task<bool> DeleteByIdAsync(int id, CancellationToken ct = default)
    {
        await using var conn = await _factory.OpenAsync(ct);
        var rows = await conn.ExecuteAsync("DELETE FROM Contacts WHERE Id = @id", new { id });
        return rows > 0;
    }

    public async Task<int> InsertAsync(Contact contact, CancellationToken ct = default)
    {
        await using var conn = await _factory.OpenAsync(ct);
        var sql = @"
            INSERT INTO Contacts (PhoneNumber, FirstName, LastName, CategoryId, CustomFieldsJson, CreatedAt)
            VALUES (@PhoneNumber, @FirstName, @LastName, @CategoryId, @CustomFieldsJson, @CreatedAt);
            SELECT last_insert_rowid();";
        
        return await conn.ExecuteScalarAsync<int>(sql, new
        {
            contact.PhoneNumber,
            contact.FirstName,
            contact.LastName,
            contact.CategoryId,
            contact.CustomFieldsJson,
            CreatedAt = contact.CreatedAt.ToString("O")
        });
    }

    public async Task<bool> UpdateAsync(int id, Contact contact, CancellationToken ct = default)
    {
        await using var conn = await _factory.OpenAsync(ct);
        var sql = @"
            UPDATE Contacts 
            SET PhoneNumber = @PhoneNumber, FirstName = @FirstName, LastName = @LastName, CategoryId = @CategoryId, CustomFieldsJson = @CustomFieldsJson
            WHERE Id = @id";
        
        var rows = await conn.ExecuteAsync(sql, new
        {
            id,
            contact.PhoneNumber,
            contact.FirstName,
            contact.LastName,
            contact.CategoryId,
            contact.CustomFieldsJson
        });
        return rows > 0;
    }

    public async Task<IReadOnlyList<string>> GetDynamicColumnsAsync(CancellationToken ct = default)
    {
        await using var conn = await _factory.OpenAsync(ct);
        var results = await conn.QueryAsync<string>("SELECT DISTINCT key FROM Contacts, json_each(CustomFieldsJson)");
        return results.AsList();
    }

    public async Task<IReadOnlyList<string>> GetDateColumnsAsync(CancellationToken ct = default)
    {
        await using var conn = await _factory.OpenAsync(ct);
        // SQLite: Find keys whose values look like dates (YYYY-MM-DD or DD-MM-YYYY)
        var results = await conn.QueryAsync<string>(@"
            SELECT DISTINCT key 
            FROM Contacts, json_each(CustomFieldsJson) 
            WHERE value LIKE '____-__-__' 
               OR value LIKE '__-__-____' 
               OR value LIKE '____/__/__' 
               OR value LIKE '__/__/____'
        ");
        return results.AsList();
    }

    public async Task<int> DeleteCustomFieldColumnAsync(string columnName, CancellationToken ct = default)
    {
        await using var conn = await _factory.OpenAsync(ct);
        
        // SQLite json_remove allows deleting a specific key from a JSON object.
        // We use JSON path format: '$.ColumnName'
        var sql = "UPDATE Contacts SET CustomFieldsJson = json_remove(CustomFieldsJson, '$.' || @columnName)";
        
        return await conn.ExecuteAsync(sql, new { columnName });
    }
}
