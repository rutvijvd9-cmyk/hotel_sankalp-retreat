namespace HotelAutomation.Data.Repositories;

using Dapper;
using HotelAutomation.Core.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public sealed class CategoryRepository
{
    private readonly SqliteConnectionFactory _factory;

    public CategoryRepository(SqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<int> AddAsync(ContactCategory category, CancellationToken ct = default)
    {
        await using var conn = await _factory.OpenAsync(ct);
        return await conn.ExecuteScalarAsync<int>("""
            INSERT INTO ContactCategories (Name, Description, CreatedAt)
            VALUES (@Name, @Description, @CreatedAt);
            SELECT last_insert_rowid();
            """, new
        {
            category.Name,
            category.Description,
            CreatedAt = category.CreatedAt.ToString("O")
        });
    }

    public async Task<IReadOnlyList<ContactCategory>> GetAllAsync(CancellationToken ct = default)
    {
        await using var conn = await _factory.OpenAsync(ct);
        var results = await conn.QueryAsync<ContactCategory>("SELECT * FROM ContactCategories ORDER BY Name");
        return results.AsList();
    }
    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        await using var conn = await _factory.OpenAsync(ct);
        var rows = await conn.ExecuteAsync("DELETE FROM ContactCategories WHERE Id = @id", new { id });
        return rows > 0;
    }
}
