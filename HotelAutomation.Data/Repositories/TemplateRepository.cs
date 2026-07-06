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
}
