namespace HotelAutomation.Worker;

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using HotelAutomation.Core.Models;
using HotelAutomation.Data;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public class DateTriggerProcessor : BackgroundService
{
    private readonly SqliteConnectionFactory _factory;
    private readonly ILogger<DateTriggerProcessor> _logger;

    public DateTriggerProcessor(SqliteConnectionFactory factory, ILogger<DateTriggerProcessor> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DateTriggerProcessor started.");
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessDateTriggersAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing date triggers.");
            }

            // Run once a day. For testing/demo purposes, we could run more frequently, 
            // but in production it's usually daily. To make it demo-friendly, we'll check every 1 hour, 
            // but ensure we only queue things once per day by recording the execution date in Outbox logic.
            // For simplicity, we just delay 24 hours.
            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }

    private async Task ProcessDateTriggersAsync(CancellationToken stoppingToken)
    {
        await using var conn = await _factory.OpenAsync(stoppingToken);
        
        // 1. Fetch all DateTrigger rules
        var rules = await conn.QueryAsync<AutomationRule>("SELECT * FROM AutomationRules WHERE TriggerEventType = 'DateTrigger' AND IsEnabled = 1");
        var ruleList = rules.AsList();
        if (ruleList.Count == 0) return;

        // 2. Fetch all contacts
        var contacts = await conn.QueryAsync("SELECT PhoneNumber, FirstName, LastName, CustomFieldsJson FROM Contacts");
        var contactList = contacts.AsList();
        
        // 3. Fetch unsubscribed contacts for quick lookup
        var unsubbedList = await conn.QueryAsync<string>("SELECT PhoneNumber FROM UnsubscribedContacts");
        var unsubbed = unsubbedList.ToHashSet();

        var today = DateTime.UtcNow.Date;

        foreach (var rule in ruleList)
        {
            var templateCategory = await conn.QueryFirstOrDefaultAsync<string>(
                "SELECT Category FROM MessageTemplates WHERE Id = @id", new { id = rule.TemplateId });

            if (string.IsNullOrWhiteSpace(rule.ConditionsJson)) continue;
            
            Dictionary<string, object> conditions;
            try
            {
                conditions = JsonSerializer.Deserialize<Dictionary<string, object>>(rule.ConditionsJson) ?? new();
            }
            catch { continue; }

            if (!conditions.TryGetValue("Column", out var colObj) || colObj == null) continue;
            var column = colObj.ToString();
            if (string.IsNullOrEmpty(column)) continue;

            int offsetDays = 0;
            if (conditions.TryGetValue("OffsetDays", out var offObj) && offObj != null)
            {
                int.TryParse(offObj.ToString(), out offsetDays);
            }

            bool ignoreYear = true; // default true for birthdays/anniversaries
            if (conditions.TryGetValue("IgnoreYear", out var iyObj) && iyObj != null)
            {
                bool.TryParse(iyObj.ToString(), out ignoreYear);
            }

            // Parse AudienceFilter
            Dictionary<string, object> audienceFilter = new();
            if (!string.IsNullOrWhiteSpace(rule.AudienceFilter))
            {
                try { audienceFilter = JsonSerializer.Deserialize<Dictionary<string, object>>(rule.AudienceFilter) ?? new(); }
                catch { }
            }

            foreach (var contact in contactList)
            {
                var phone = (string)(contact.PhoneNumber ?? "");
                if (string.IsNullOrWhiteSpace(phone)) continue;

                if (templateCategory == "Marketing" && unsubbed.Contains(phone)) continue;

                var customJson = (string)(contact.CustomFieldsJson ?? "{}");
                Dictionary<string, object> fields;
                try { fields = JsonSerializer.Deserialize<Dictionary<string, object>>(customJson) ?? new(); }
                catch { continue; }

                // Apply AudienceFilter if specified
                if (audienceFilter.TryGetValue("Type", out var filterTypeObj) && filterTypeObj != null)
                {
                    var filterType = filterTypeObj.ToString();
                    if (filterType == "Custom" && audienceFilter.TryGetValue("Column", out var fColObj) && audienceFilter.TryGetValue("Value", out var fValObj))
                    {
                        var filterColumn = fColObj?.ToString();
                        var filterValue = fValObj?.ToString();
                        if (!string.IsNullOrEmpty(filterColumn) && !string.IsNullOrEmpty(filterValue))
                        {
                            if (!fields.TryGetValue(filterColumn, out var cVal) || cVal?.ToString() != filterValue)
                            {
                                continue; // Does not match filter
                            }
                        }
                    }
                }

                if (!fields.TryGetValue(column, out var dateObj) || dateObj == null) continue;
                var dateStr = dateObj.ToString();
                
                if (DateTime.TryParse(dateStr, out var targetDate))
                {
                    // Adjust by offset days (e.g., -30 days means trigger 30 days BEFORE)
                    // If target is May 15, and offset is -30, we want to trigger on April 15.
                    var triggerDate = targetDate.AddDays(offsetDays);

                    bool isMatch = false;
                    if (ignoreYear)
                    {
                        if (triggerDate.Month == today.Month && triggerDate.Day == today.Day)
                            isMatch = true;
                    }
                    else
                    {
                        if (triggerDate.Date == today)
                            isMatch = true;
                    }

                    if (isMatch)
                    {
                        // Queue message!
                        // In a real app we'd check if we already queued this today to prevent duplicates.
                        // For MVP, we insert.
                        
                        var baseParams = new Dictionary<string, object?>
                        {
                            ["Name"] = ((string?)contact.FirstName ?? "") + " " + ((string?)contact.LastName ?? ""),
                            ["FirstName"] = contact.FirstName ?? "",
                            ["LastName"] = contact.LastName ?? ""
                        };
                        foreach (var kv in fields) baseParams[kv.Key] = kv.Value;

                        var parametersJson = JsonSerializer.Serialize(baseParams);

                        await conn.ExecuteAsync(@"
                            INSERT INTO OutboxMessages (ToPhoneNumber, TemplateId, ParametersJson, Status, RetryCount, CreatedAt, RuleId)
                            VALUES (@ToPhoneNumber, @TemplateId, @ParametersJson, 'Pending', 0, @CreatedAt, @RuleId)", new
                        {
                            ToPhoneNumber = phone,
                            TemplateId = rule.TemplateId,
                            ParametersJson = parametersJson,
                            CreatedAt = DateTimeOffset.UtcNow.ToString("O"),
                            RuleId = rule.Id
                        });
                        
                        _logger.LogInformation("Queued date trigger message for {Phone} (Rule {RuleId})", phone, rule.Id);
                    }
                }
            }
        }
    }
}
