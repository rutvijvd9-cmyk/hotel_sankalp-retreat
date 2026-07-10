namespace HotelAutomation.Data.Repositories;

using Dapper;
using HotelAutomation.Core.Interfaces;
using HotelAutomation.Core.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public sealed class CampaignRepository : ICampaignRepository
{
    private readonly SqliteConnectionFactory _factory;

    public CampaignRepository(SqliteConnectionFactory factory) => _factory = factory;

    public async Task<IReadOnlyList<Campaign>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var conn = await _factory.OpenAsync(cancellationToken);
        
        var sql = @"
            SELECT * FROM Campaigns ORDER BY CreatedAt DESC;
            SELECT * FROM CampaignSteps ORDER BY StepNumber ASC;
        ";
        
        using var multi = await conn.QueryMultipleAsync(sql);
        var campaigns = (await multi.ReadAsync<Campaign>()).ToList();
        var steps = (await multi.ReadAsync<CampaignStep>()).ToList();
        
        foreach (var campaign in campaigns)
        {
            campaign.Steps.AddRange(steps.Where(s => s.CampaignId == campaign.Id));
        }
        
        return campaigns;
    }

    public async Task<Campaign?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var conn = await _factory.OpenAsync(cancellationToken);
        
        var sql = @"
            SELECT * FROM Campaigns WHERE Id = @id;
            SELECT * FROM CampaignSteps WHERE CampaignId = @id ORDER BY StepNumber ASC;
        ";
        
        using var multi = await conn.QueryMultipleAsync(sql, new { id });
        var campaign = await multi.ReadSingleOrDefaultAsync<Campaign>();
        
        if (campaign != null)
        {
            var steps = await multi.ReadAsync<CampaignStep>();
            campaign.Steps.AddRange(steps);
        }
        
        return campaign;
    }

    public async Task<int> CreateAsync(Campaign campaign, CancellationToken cancellationToken = default)
    {
        await using var conn = await _factory.OpenAsync(cancellationToken);
        using var transaction = conn.BeginTransaction();
        
        var sql = @"
            INSERT INTO Campaigns (Name, IsEnabled, CreatedAt)
            VALUES (@Name, @IsEnabled, @CreatedAt);
            SELECT last_insert_rowid();";
            
        var id = await conn.ExecuteScalarAsync<int>(sql, new
        {
            campaign.Name,
            campaign.IsEnabled,
            CreatedAt = campaign.CreatedAt.ToString("O")
        }, transaction);

        if (campaign.Steps.Any())
        {
            var stepSql = @"
                INSERT INTO CampaignSteps (CampaignId, StepNumber, TemplateId, DelayMinutes, CreatedAt)
                VALUES (@CampaignId, @StepNumber, @TemplateId, @DelayMinutes, @CreatedAt);";
                
            await conn.ExecuteAsync(stepSql, campaign.Steps.Select(s => new
            {
                CampaignId = id,
                s.StepNumber,
                s.TemplateId,
                s.DelayMinutes,
                CreatedAt = s.CreatedAt.ToString("O")
            }), transaction);
        }

        transaction.Commit();
        return id;
    }

    public async Task<bool> UpdateAsync(int id, Campaign campaign, CancellationToken cancellationToken = default)
    {
        await using var conn = await _factory.OpenAsync(cancellationToken);
        using var transaction = conn.BeginTransaction();

        var sql = "UPDATE Campaigns SET Name = @Name, IsEnabled = @IsEnabled WHERE Id = @id";
        var rows = await conn.ExecuteAsync(sql, new { campaign.Name, campaign.IsEnabled, id }, transaction);
        
        if (rows > 0)
        {
            // Fully replace steps for simplicity
            await conn.ExecuteAsync("DELETE FROM CampaignSteps WHERE CampaignId = @id", new { id }, transaction);
            
            if (campaign.Steps.Any())
            {
                var stepSql = @"
                    INSERT INTO CampaignSteps (CampaignId, StepNumber, TemplateId, DelayMinutes, CreatedAt)
                    VALUES (@CampaignId, @StepNumber, @TemplateId, @DelayMinutes, @CreatedAt);";
                    
                await conn.ExecuteAsync(stepSql, campaign.Steps.Select(s => new
                {
                    CampaignId = id,
                    s.StepNumber,
                    s.TemplateId,
                    s.DelayMinutes,
                    CreatedAt = s.CreatedAt.ToString("O")
                }), transaction);
            }
        }

        transaction.Commit();
        return rows > 0;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var conn = await _factory.OpenAsync(cancellationToken);
        using var transaction = conn.BeginTransaction();
        
        await conn.ExecuteAsync("DELETE FROM ContactCampaignProgress WHERE CampaignStepId IN (SELECT Id FROM CampaignSteps WHERE CampaignId = @id)", new { id }, transaction);
        await conn.ExecuteAsync("DELETE FROM CampaignSteps WHERE CampaignId = @id", new { id }, transaction);
        var rows = await conn.ExecuteAsync("DELETE FROM Campaigns WHERE Id = @id", new { id }, transaction);
        
        transaction.Commit();
        return rows > 0;
    }
}
