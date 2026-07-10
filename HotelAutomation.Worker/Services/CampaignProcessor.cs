namespace HotelAutomation.Worker.Services;

using Dapper;
using HotelAutomation.Core.Models;
using HotelAutomation.Data;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public class CampaignProcessor : BackgroundService
{
    private readonly SqliteConnectionFactory _factory;
    private readonly ILogger<CampaignProcessor> _logger;

    public CampaignProcessor(SqliteConnectionFactory factory, ILogger<CampaignProcessor> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessDueCampaignStepsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing campaign steps.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task ProcessDueCampaignStepsAsync(CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        using var transaction = conn.BeginTransaction();

        // 1. Find all pending progress records that are due, check category subscriptions
        var sql = @"
            SELECT p.ContactId, p.CampaignStepId, p.Status, p.NextExecutionTime,
                   c.PhoneNumber, c.CustomFieldsJson,
                   s.TemplateId, s.CampaignId, s.StepNumber,
                   COALESCE(cc.IsSubscribed, 1) as IsSubscribed
            FROM ContactCampaignProgress p
            JOIN Contacts c ON c.Id = p.ContactId
            JOIN CampaignSteps s ON s.Id = p.CampaignStepId
            JOIN Campaigns cmp ON cmp.Id = s.CampaignId
            JOIN MessageTemplates t ON t.Id = s.TemplateId
            LEFT JOIN ContactCategories cat ON cat.Name = t.Category
            LEFT JOIN ContactConsent cc ON cc.ContactId = c.Id AND cc.CategoryId = cat.Id
            WHERE p.Status = 'Pending' 
              AND p.NextExecutionTime <= @Now
              AND cmp.IsEnabled = 1;
        ";

        var dueSteps = (await conn.QueryAsync(sql, new { Now = DateTimeOffset.UtcNow.ToString("O") }, transaction)).ToList();

        if (dueSteps.Count == 0)
        {
            transaction.Commit();
            return;
        }

        foreach (var step in dueSteps)
        {
            if (step.IsSubscribed == 1)
            {
                // Insert into Outbox
                var outboxSql = @"
                    INSERT INTO OutboxMessages (ToPhoneNumber, TemplateId, ParametersJson, Status, CreatedAt)
                    VALUES (@ToPhoneNumber, @TemplateId, @ParametersJson, 'Pending', @CreatedAt);
                ";
                await conn.ExecuteAsync(outboxSql, new
                {
                    ToPhoneNumber = step.PhoneNumber,
                    TemplateId = step.TemplateId,
                    ParametersJson = step.CustomFieldsJson, // Merge custom fields directly for variables
                    CreatedAt = DateTimeOffset.UtcNow.ToString("O")
                }, transaction);
            }
            else
            {
                _logger.LogInformation("Skipping campaign step for contact {ContactId} due to category opt-out.", (long)step.ContactId);
            }

            // Mark current step as Sent (even if skipped, to progress the campaign)
            await conn.ExecuteAsync(@"
                UPDATE ContactCampaignProgress SET Status = 'Sent'
                WHERE ContactId = @ContactId AND CampaignStepId = @CampaignStepId;
            ", new { step.ContactId, step.CampaignStepId }, transaction);

            // Find next step in the campaign
            var nextStep = await conn.QuerySingleOrDefaultAsync<CampaignStep>(@"
                SELECT * FROM CampaignSteps 
                WHERE CampaignId = @CampaignId AND StepNumber > @StepNumber
                ORDER BY StepNumber ASC LIMIT 1;
            ", new { CampaignId = step.CampaignId, StepNumber = step.StepNumber }, transaction);

            if (nextStep != null)
            {
                var nextTime = DateTimeOffset.UtcNow.AddMinutes(nextStep.DelayMinutes).ToString("O");
                await conn.ExecuteAsync(@"
                    INSERT INTO ContactCampaignProgress (ContactId, CampaignStepId, Status, NextExecutionTime)
                    VALUES (@ContactId, @CampaignStepId, 'Pending', @NextExecutionTime);
                ", new { step.ContactId, CampaignStepId = nextStep.Id, NextExecutionTime = nextTime }, transaction);
            }
        }

        transaction.Commit();
        _logger.LogInformation("Processed {Count} due campaign steps.", dueSteps.Count);
    }
}
