namespace HotelAutomation.Data.Repositories;

using Dapper;
using HotelAutomation.Core.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public sealed class TemplateVariantRepository
{
    private readonly SqliteConnectionFactory _factory;

    public TemplateVariantRepository(SqliteConnectionFactory factory) => _factory = factory;

    public async Task<List<TemplateVariant>> GetByTemplateIdAsync(int templateId, CancellationToken ct = default)
    {
        await using var conn = await _factory.OpenAsync(ct);
        var results = await conn.QueryAsync<TemplateVariant>(
            "SELECT * FROM TemplateVariants WHERE TemplateId = @TemplateId", new { TemplateId = templateId });
        return results.AsList();
    }

    public async Task AddAsync(TemplateVariant variant, CancellationToken ct = default)
    {
        await using var conn = await _factory.OpenAsync(ct);
        var sql = @"
            INSERT INTO TemplateVariants (TemplateId, VariantName, Body, Weight, CreatedAt)
            VALUES (@TemplateId, @VariantName, @Body, @Weight, @CreatedAt)";
        
        await conn.ExecuteAsync(sql, new
        {
            variant.TemplateId,
            variant.VariantName,
            variant.Body,
            variant.Weight,
            CreatedAt = variant.CreatedAt.ToString("O")
        });
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await using var conn = await _factory.OpenAsync(ct);
        await conn.ExecuteAsync("DELETE FROM TemplateVariants WHERE Id = @Id", new { Id = id });
    }
}
