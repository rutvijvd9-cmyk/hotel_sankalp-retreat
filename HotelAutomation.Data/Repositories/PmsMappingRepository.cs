namespace HotelAutomation.Data.Repositories;

using Dapper;
using HotelAutomation.Core.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public sealed class PmsMappingRepository
{
    private readonly SqliteConnectionFactory _factory;

    public PmsMappingRepository(SqliteConnectionFactory factory) => _factory = factory;

    public async Task<List<PmsMapping>> GetAllAsync(CancellationToken ct = default)
    {
        await using var conn = await _factory.OpenAsync(ct);
        var results = await conn.QueryAsync<PmsMapping>("SELECT * FROM PmsMappings ORDER BY CreatedAt DESC");
        return results.AsList();
    }

    public async Task<PmsMapping?> GetByNameAsync(string pmsSystemName, CancellationToken ct = default)
    {
        await using var conn = await _factory.OpenAsync(ct);
        return await conn.QueryFirstOrDefaultAsync<PmsMapping>(
            "SELECT * FROM PmsMappings WHERE PmsSystemName = @PmsSystemName COLLATE NOCASE",
            new { PmsSystemName = pmsSystemName });
    }

    public async Task AddAsync(PmsMapping mapping, CancellationToken ct = default)
    {
        await using var conn = await _factory.OpenAsync(ct);
        var sql = @"
            INSERT INTO PmsMappings (PmsSystemName, JsonPathMapping, CreatedAt)
            VALUES (@PmsSystemName, @JsonPathMapping, @CreatedAt)";
        
        await conn.ExecuteAsync(sql, new
        {
            mapping.PmsSystemName,
            mapping.JsonPathMapping,
            CreatedAt = mapping.CreatedAt.ToString("O")
        });
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await using var conn = await _factory.OpenAsync(ct);
        await conn.ExecuteAsync("DELETE FROM PmsMappings WHERE Id = @Id", new { Id = id });
    }
}
