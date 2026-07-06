namespace HotelAutomation.Data.Repositories;

using Dapper;
using HotelAutomation.Core.Interfaces;
using HotelAutomation.Core.Models;

public sealed class TemplateRepository : ITemplateRepository
{
    private readonly SqliteConnectionFactory _factory;

    public TemplateRepository(SqliteConnectionFactory factory) => _factory = factory;

    public async Task<MessageTemplate?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        await using var conn = await _factory.OpenAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<MessageTemplate>(
            "SELECT * FROM MessageTemplates WHERE Id = @id", new { id });
    }

    public async Task<IReadOnlyList<MessageTemplate>> GetAllAsync(CancellationToken ct = default)
    {
        await using var conn = await _factory.OpenAsync(ct);
        var results = await conn.QueryAsync<MessageTemplate>("SELECT * FROM MessageTemplates");
        return results.AsList();
    }

    public async Task<int> AddAsync(MessageTemplate template, CancellationToken ct = default)
    {
        await using var conn = await _factory.OpenAsync(ct);
        var sql = @"
            INSERT INTO MessageTemplates (Name, ExternalTemplateName, LanguageCode, ChannelType, CreatedAt)
            VALUES (@Name, @ExternalTemplateName, @LanguageCode, @ChannelType, @CreatedAt);
            SELECT last_insert_rowid();";
        
        return await conn.ExecuteScalarAsync<int>(sql, new
        {
            template.Name,
            template.ExternalTemplateName,
            template.LanguageCode,
            template.ChannelType,
            CreatedAt = DateTimeOffset.UtcNow.ToString("O")
        });
    }

    public async Task<bool> UpdateAsync(int id, MessageTemplate template, CancellationToken ct = default)
    {
        await using var conn = await _factory.OpenAsync(ct);
        var sql = @"
            UPDATE MessageTemplates 
            SET Name = @Name, ExternalTemplateName = @ExternalTemplateName, LanguageCode = @LanguageCode, ChannelType = @ChannelType
            WHERE Id = @id";
        
        var rows = await conn.ExecuteAsync(sql, new
        {
            id,
            template.Name,
            template.ExternalTemplateName,
            template.LanguageCode,
            template.ChannelType
        });
        return rows > 0;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        await using var conn = await _factory.OpenAsync(ct);
        // Ensure no automation rules are using this template before deleting (or cascade)
        // For simplicity, we just delete
        var sql = "DELETE FROM MessageTemplates WHERE Id = @id";
        var rows = await conn.ExecuteAsync(sql, new { id });
        return rows > 0;
    }
}
