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
    private readonly Services.VariantSelector _variantSelector;

    public DateTriggerProcessor(SqliteConnectionFactory factory, ILogger<DateTriggerProcessor> logger, Services.VariantSelector variantSelector)
    {
        _factory = factory;
        _logger = logger;
        _variantSelector = variantSelector;
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
            try
            {
                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
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
        
        // 3. Fetch category-level unsubscribed contacts for quick lookup
        // We will store a dictionary of PhoneNumber -> HashSet<CategoryId> for opted-out categories
        var unsubQuery = @"
            SELECT c.PhoneNumber, cc.CategoryId 
            FROM ContactConsent cc
            JOIN Contacts c ON c.Id = cc.ContactId
            WHERE cc.IsSubscribed = 0";
        var unsubData = await conn.QueryAsync(unsubQuery);
        var unsubbed = new Dictionary<string, HashSet<long>>();
        foreach (var u in unsubData)
        {
            var phone = (string)u.PhoneNumber;
            var catId = (long)u.CategoryId;
            if (!unsubbed.ContainsKey(phone)) unsubbed[phone] = new HashSet<long>();
            unsubbed[phone].Add(catId);
        }

        var today = DateTime.UtcNow.Date;

        foreach (var rule in ruleList)
        {
            var templateInfo = await conn.QueryFirstOrDefaultAsync(
                "SELECT Category, CategoryId FROM MessageTemplates t LEFT JOIN ContactCategories c ON c.Name = t.Category WHERE t.Id = @id", new { id = rule.TemplateId });

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

                if (templateInfo != null)
                {
                    string? category = templateInfo.Category as string;
                    if (category != "Utility")
                    {
                        var catIdObj = templateInfo.CategoryId;
                        if (catIdObj != null)
                        {
                            var catId = (long)catIdObj;
                            if (unsubbed.ContainsKey(phone) && unsubbed[phone].Contains(catId))
                            {
                                continue; // User unsubscribed from this category
                            }
                        }
                    }
                }

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
                        var variantId = await _variantSelector.SelectVariantIdAsync(rule.TemplateId);

                        await conn.ExecuteAsync(@"
                            INSERT INTO OutboxMessages (ToPhoneNumber, TemplateId, VariantId, ParametersJson, Status, RetryCount, CreatedAt, RuleId)
                            VALUES (@ToPhoneNumber, @TemplateId, @VariantId, @ParametersJson, 'Pending', 0, @CreatedAt, @RuleId)", new
                        {
                            ToPhoneNumber = phone,
                            TemplateId = rule.TemplateId,
                            VariantId = variantId,
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
