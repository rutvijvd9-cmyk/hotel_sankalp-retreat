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
            INSERT INTO MessageTemplates (Name, ExternalTemplateName, LanguageCode, ChannelType, BodyParamCount, Category, CreatedAt)
            VALUES (@Name, @ExternalTemplateName, @LanguageCode, @ChannelType, @BodyParamCount, @Category, @CreatedAt);
            SELECT last_insert_rowid();";
        
        return await conn.ExecuteScalarAsync<int>(sql, new
        {
            template.Name,
            template.ExternalTemplateName,
            template.LanguageCode,
            template.ChannelType,
            template.BodyParamCount,
            template.Category,
            CreatedAt = DateTimeOffset.UtcNow.ToString("O")
        });
    }

    public async Task<bool> UpdateAsync(int id, MessageTemplate template, CancellationToken ct = default)
    {
        await using var conn = await _factory.OpenAsync(ct);
        var sql = @"
            UPDATE MessageTemplates 
            SET Name = @Name, ExternalTemplateName = @ExternalTemplateName, LanguageCode = @LanguageCode, ChannelType = @ChannelType, BodyParamCount = @BodyParamCount, Category = @Category
            WHERE Id = @id";
        
        var rows = await conn.ExecuteAsync(sql, new
        {
            id,
            template.Name,
            template.ExternalTemplateName,
            template.LanguageCode,
            template.ChannelType,
            template.BodyParamCount,
            template.Category
        });
        return rows > 0;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        await using var conn = await _factory.OpenAsync(ct);
        
        // Check if used in AutomationRules
        var ruleName = await conn.QueryFirstOrDefaultAsync<string>("SELECT Name FROM AutomationRules WHERE TemplateId = @id LIMIT 1", new { id });
        if (!string.IsNullOrEmpty(ruleName))
        {
            throw new InvalidOperationException($"Cannot delete template because it is used in the automation rule '{ruleName}'. Please remove or change the rule first.");
        }

        // Check if used in Campaigns
        var campaignName = await conn.QueryFirstOrDefaultAsync<string>("SELECT Name FROM Campaigns WHERE TemplateId = @id LIMIT 1", new { id });
        if (!string.IsNullOrEmpty(campaignName))
        {
            throw new InvalidOperationException($"Cannot delete template because it is used in the campaign '{campaignName}'.");
        }

        // Check if used in OutboxMessages
        var hasOutbox = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM OutboxMessages WHERE TemplateId = @id LIMIT 1", new { id }) > 0;
        if (hasOutbox)
        {
            throw new InvalidOperationException("Cannot delete template because it is referenced by past or pending outbox messages.");
        }

        try
        {
            var sql = "DELETE FROM MessageTemplates WHERE Id = @id";
            var rows = await conn.ExecuteAsync(sql, new { id });
            return rows > 0;
        }
        catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.SqliteErrorCode == 19)
        {
            throw new InvalidOperationException("Cannot delete template because it is currently in use.");
        }
    }
}
