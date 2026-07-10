namespace HotelAutomation.Data.Repositories;

using Dapper;
using HotelAutomation.Core.Interfaces;
using HotelAutomation.Core.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public sealed class SmartListRepository : ISmartListRepository
{
    private readonly SqliteConnectionFactory _factory;

    public SmartListRepository(SqliteConnectionFactory factory) => _factory = factory;

    public async Task<IReadOnlyList<SmartList>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var conn = await _factory.OpenAsync(cancellationToken);
        var results = await conn.QueryAsync<SmartList>("SELECT * FROM SmartLists ORDER BY CreatedAt DESC");
        return results.AsList();
    }

    public async Task<SmartList?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var conn = await _factory.OpenAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<SmartList>(
            "SELECT * FROM SmartLists WHERE Id = @id", new { id });
    }

    public async Task<int> AddAsync(SmartList list, CancellationToken cancellationToken = default)
    {
        await using var conn = await _factory.OpenAsync(cancellationToken);
        var sql = @"
            INSERT INTO SmartLists (ListName, FilterConditionsJson, CreatedAt)
            VALUES (@ListName, @FilterConditionsJson, @CreatedAt);
            SELECT last_insert_rowid();";
        
        return await conn.ExecuteScalarAsync<int>(sql, new
        {
            list.ListName,
            list.FilterConditionsJson,
            CreatedAt = list.CreatedAt.ToString("O")
        });
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var conn = await _factory.OpenAsync(cancellationToken);
        var rows = await conn.ExecuteAsync("DELETE FROM SmartLists WHERE Id = @id", new { id });
        return rows > 0;
    }
}
