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
            INSERT INTO Contacts (PhoneNumber, FirstName, LastName, CategoryId, CustomFieldsJson, CreatedAt, UpdatedAt, CheckInCount, AverageStayDays, IsVip)
            VALUES (@PhoneNumber, @FirstName, @LastName, @CategoryId, @CustomFieldsJson, @CreatedAt, @UpdatedAt, @CheckInCount, @AverageStayDays, @IsVip)
            ON CONFLICT(PhoneNumber) DO UPDATE SET
                FirstName = excluded.FirstName,
                LastName = excluded.LastName,
                CategoryId = excluded.CategoryId,
                CustomFieldsJson = excluded.CustomFieldsJson,
                UpdatedAt = excluded.UpdatedAt,
                UpdateCount = Contacts.UpdateCount + 1;
            """, new
        {
            contact.PhoneNumber,
            contact.FirstName,
            contact.LastName,
            contact.CategoryId,
            contact.CustomFieldsJson,
            CreatedAt = contact.CreatedAt.ToString("O"),
            UpdatedAt = DateTimeOffset.UtcNow.ToString("O"),
            contact.CheckInCount,
            contact.AverageStayDays,
            IsVip = contact.IsVip ? 1 : 0
        });
    }

    public async Task UpsertBulkAsync(IEnumerable<Contact> contacts, CancellationToken ct = default)
    {
        await using var conn = await _factory.OpenAsync(ct);
        using var transaction = conn.BeginTransaction();
        
        var sql = """
            INSERT INTO Contacts (PhoneNumber, FirstName, LastName, CategoryId, CustomFieldsJson, CreatedAt, UpdatedAt, CheckInCount, AverageStayDays, IsVip)
            VALUES (@PhoneNumber, @FirstName, @LastName, @CategoryId, @CustomFieldsJson, @CreatedAt, @UpdatedAt, @CheckInCount, @AverageStayDays, @IsVip)
            ON CONFLICT(PhoneNumber) DO UPDATE SET
                FirstName = excluded.FirstName,
                LastName = excluded.LastName,
                CategoryId = excluded.CategoryId,
                CustomFieldsJson = excluded.CustomFieldsJson,
                UpdatedAt = excluded.UpdatedAt,
                UpdateCount = Contacts.UpdateCount + 1,
                CheckInCount = excluded.CheckInCount,
                AverageStayDays = excluded.AverageStayDays,
                IsVip = excluded.IsVip;
            """;

        await conn.ExecuteAsync(sql, contacts.Select(c => new
        {
            c.PhoneNumber,
            c.FirstName,
            c.LastName,
            c.CategoryId,
            c.CustomFieldsJson,
            CreatedAt = c.CreatedAt.ToString("O"),
            UpdatedAt = DateTimeOffset.UtcNow.ToString("O"),
            c.CheckInCount,
            c.AverageStayDays,
            IsVip = c.IsVip ? 1 : 0
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

    public async Task<(IReadOnlyList<Contact> Contacts, int TotalCount)> GetBySmartListAsync(SmartList smartList, int page = 1, int limit = 50, CancellationToken ct = default)
    {
        await using var conn = await _factory.OpenAsync(ct);
        int offset = (page - 1) * limit;

        // Parse JSON AST (e.g. [{"field": "City", "operator": "equals", "value": "Hamilton"}])
        // Simplified dynamic query generator
        var filters = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, string>>>(smartList.FilterConditionsJson) ?? new();
        
        var whereClauses = new List<string>();
        var parameters = new DynamicParameters();
        
        parameters.Add("limit", limit);
        parameters.Add("offset", offset);

        int pIndex = 0;
        foreach (var filter in filters)
        {
            if (filter.TryGetValue("field", out var field) && filter.TryGetValue("operator", out var op) && filter.TryGetValue("value", out var val))
            {
                var pName = $"@p{pIndex++}";
                
                // If it's a known top-level field
                if (field == "City" || field == "State" || field == "Country") 
                {
                    // Fallback to JSON extraction for custom fields
                    string sqliteOp = op == "equals" ? "=" : (op == "contains" ? "LIKE" : "=");
                    if (op == "contains") val = $"%{val}%";
                    
                    whereClauses.Add($"json_extract(CustomFieldsJson, '$.{field}') {sqliteOp} {pName}");
                    parameters.Add(pName, val);
                }
                else if (field == "CategoryId")
                {
                    whereClauses.Add($"CategoryId = {pName}");
                    parameters.Add(pName, val);
                }
            }
        }

        string baseQuery = "FROM Contacts";
        if (whereClauses.Count > 0)
        {
            baseQuery += " WHERE " + string.Join(" AND ", whereClauses);
        }

        var totalCount = await conn.ExecuteScalarAsync<int>($"SELECT COUNT(*) {baseQuery}", parameters);
        var results = await conn.QueryAsync<Contact>($"SELECT * {baseQuery} ORDER BY CreatedAt DESC LIMIT @limit OFFSET @offset", parameters);

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
            INSERT INTO Contacts (PhoneNumber, FirstName, LastName, CategoryId, CustomFieldsJson, CreatedAt, UpdatedAt, CheckInCount, AverageStayDays, IsVip)
            VALUES (@PhoneNumber, @FirstName, @LastName, @CategoryId, @CustomFieldsJson, @CreatedAt, @UpdatedAt, @CheckInCount, @AverageStayDays, @IsVip);
            SELECT last_insert_rowid();";
        
        return await conn.ExecuteScalarAsync<int>(sql, new
        {
            contact.PhoneNumber,
            contact.FirstName,
            contact.LastName,
            contact.CategoryId,
            contact.CustomFieldsJson,
            CreatedAt = contact.CreatedAt.ToString("O"),
            UpdatedAt = DateTimeOffset.UtcNow.ToString("O")
        });
    }

    public async Task<bool> UpdateAsync(int id, Contact contact, CancellationToken ct = default)
    {
        await using var conn = await _factory.OpenAsync(ct);
        var sql = @"
            UPDATE Contacts 
            SET PhoneNumber = @PhoneNumber, FirstName = @FirstName, LastName = @LastName, CategoryId = @CategoryId, CustomFieldsJson = @CustomFieldsJson, UpdatedAt = @UpdatedAt, UpdateCount = UpdateCount + 1, CheckInCount = @CheckInCount, AverageStayDays = @AverageStayDays, IsVip = @IsVip
            WHERE Id = @id";
        
        var rows = await conn.ExecuteAsync(sql, new
        {
            id,
            contact.PhoneNumber,
            contact.FirstName,
            contact.LastName,
            contact.CategoryId,
            contact.CustomFieldsJson,
            UpdatedAt = DateTimeOffset.UtcNow.ToString("O"),
            contact.CheckInCount,
            contact.AverageStayDays,
            IsVip = contact.IsVip ? 1 : 0
        });
        return rows > 0;
    }

    public async Task<Contact?> GetByPhoneNumberAsync(string phoneNumber, CancellationToken ct = default)
    {
        await using var conn = await _factory.OpenAsync(ct);
        return await conn.QueryFirstOrDefaultAsync<Contact>("SELECT * FROM Contacts WHERE PhoneNumber = @PhoneNumber", new { PhoneNumber = phoneNumber });
    }

    public async Task<IEnumerable<Contact>> SearchContactsAsync(string query, CancellationToken ct = default)
    {
        await using var conn = await _factory.OpenAsync(ct);
        return await conn.QueryAsync<Contact>(@"
            SELECT * FROM Contacts 
            WHERE PhoneNumber LIKE @query 
               OR FirstName LIKE @query 
               OR LastName LIKE @query 
               OR (FirstName || ' ' || LastName) LIKE @query
            ORDER BY CreatedAt DESC
            LIMIT 50", new { query = $"%{query}%" });
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
