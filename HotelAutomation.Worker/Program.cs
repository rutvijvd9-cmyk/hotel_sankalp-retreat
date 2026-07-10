using HotelAutomation.Connectors.Pms;
using HotelAutomation.Connectors.WhatsApp;
using HotelAutomation.Core.Interfaces;
using HotelAutomation.Data;
using HotelAutomation.Data.Repositories;
using HotelAutomation.Worker;
using HotelAutomation.Worker.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;
using Dapper;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options => 
{
    options.Limits.MaxRequestBodySize = 250_000_000; // 250 MB
});

builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 250_000_000; // 250 MB
});

// Enable CORS for the React UI
builder.Services.AddCors(options => {
    options.AddDefaultPolicy(policy => {
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

// ── Database ──────────────────────────────────────────────────────────────────
var connectionString = builder.Configuration.GetConnectionString("Sqlite")
    ?? throw new InvalidOperationException("Missing ConnectionStrings:Sqlite in config.");

builder.Services.AddSingleton(new SqliteConnectionFactory(connectionString));

// ── Repositories ──────────────────────────────────────────────────────────────
builder.Services.AddSingleton<ITemplateRepository, TemplateRepository>();
builder.Services.AddSingleton<ISmartListRepository, SmartListRepository>();
builder.Services.AddSingleton<ICampaignRepository, CampaignRepository>();
builder.Services.AddSingleton<IRuleRepository, RuleRepository>();
builder.Services.AddSingleton<IOutboxRepository, OutboxRepository>();
builder.Services.AddSingleton<LinkShortenerService>();
builder.Services.AddSingleton<EventLogRepository>();
builder.Services.AddSingleton<JobRepository>();
builder.Services.AddSingleton<InboxRepository>();
builder.Services.AddSingleton<CategoryRepository>();
builder.Services.AddSingleton<ContactRepository>();
builder.Services.AddSingleton<CsvImportService>();
builder.Services.AddSingleton<BroadcastRepository>();
builder.Services.AddSingleton<UnsubscribedContactRepository>();
builder.Services.AddSingleton<ContactConsentRepository>();
builder.Services.AddSingleton<PmsMappingRepository>();
builder.Services.AddSingleton<TemplateVariantRepository>();
builder.Services.AddSingleton<HotelAutomation.Worker.Services.VariantSelector>();

// ── WhatsApp Connector ────────────────────────────────────────────────────────
builder.Services.Configure<WhatsAppOptions>(
    builder.Configuration.GetSection(WhatsAppOptions.SectionName));

builder.Services.AddHttpClient<WhatsAppApiClient>();
builder.Services.AddSingleton<IMessageChannel, WhatsAppMessageSender>();
builder.Services.AddSingleton<IMessageChannel, SmsMessageSender>();

// ── PMS Connector ─────────────────────────────────────────────────────────────
var pmsType = builder.Configuration.GetValue<string>("PmsConnectorType") ?? "Manual";
if (pmsType == "Database")
{
    var pmsOptions = new DatabasePmsOptions();
    builder.Configuration.GetSection(DatabasePmsOptions.SectionName).Bind(pmsOptions);
    builder.Services.AddSingleton(pmsOptions);
    builder.Services.AddSingleton<IPmsEventSource>(sp => 
        new DatabasePmsConnector(
            sp.GetRequiredService<DatabasePmsOptions>(),
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<DatabasePmsConnector>>(),
            builder.Configuration.GetConnectionString("Sqlite")!
        ));
}
else
{
    builder.Services.AddSingleton<ManualPmsConnector>();
    builder.Services.AddSingleton<IPmsEventSource>(sp => sp.GetRequiredService<ManualPmsConnector>());
}

// ── PDF Generation ─────────────────────────────────────────────────────────────
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
builder.Services.AddSingleton<IPdfGenerator, HotelAutomation.Worker.Services.InvoicePdfGenerator>();

// ── Hosted Services (Background Engine) ───────────────────────────────────────
builder.Services.AddHostedService<HotelWorker>();
builder.Services.AddHostedService<OutboxProcessor>();
builder.Services.AddHostedService<JobProcessor>();
builder.Services.AddHostedService<CampaignProcessor>();
builder.Services.AddHostedService<DateTriggerProcessor>();
builder.Services.AddHostedService<WebhookRelayPoller>();

var app = builder.Build();
app.UseCors();

app.UseDefaultFiles();
app.UseStaticFiles();

// ── API Endpoints for UI ──────────────────────────────────────────────────────
app.MapGet("/api/templates", async (ITemplateRepository repo, int page = 1, int limit = 50) => 
{
    var (list, total) = await repo.GetAllPaginatedAsync(page, limit);
    return Results.Ok(new { Templates = list, TotalCount = total });
});

app.MapGet("/api/rules", async (SqliteConnectionFactory factory) => 
{
    using var conn = await factory.OpenAsync();
    var rules = await conn.QueryAsync<HotelAutomation.Core.Models.AutomationRule>("SELECT * FROM AutomationRules");
    return rules;
});

app.MapGet("/api/outbox", async (SqliteConnectionFactory factory) => 
{
    using var conn = await factory.OpenAsync();
    var outbox = await conn.QueryAsync("SELECT * FROM OutboxMessages ORDER BY CreatedAt DESC LIMIT 50");
    return outbox;
});

app.MapGet("/api/logs", async (EventLogRepository repo) => 
{
    return await repo.GetRecentLogsAsync(100);
});

// Boilerplate stubs to support UI Builder saves if not implemented
app.MapPost("/api/templates", async (HotelAutomation.Core.Models.MessageTemplate template, ITemplateRepository repo) => 
{
    var trimmed = template with 
    { 
        Name = template.Name.Trim(), 
        ExternalTemplateName = template.ExternalTemplateName.Trim(), 
        LanguageCode = template.LanguageCode?.Trim() ?? "en" 
    };

    int paramCount = trimmed.BodyParamCount;
    if (!string.IsNullOrEmpty(trimmed.ParameterDefinitionsJson))
    {
        try
        {
            var defs = System.Text.Json.JsonSerializer.Deserialize<List<HotelAutomation.Core.Models.ParameterDefinition>>(trimmed.ParameterDefinitionsJson, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (defs != null)
            {
                paramCount = defs.Count;
            }
        }
        catch { }
    }

    var finalTemplate = trimmed with { BodyParamCount = paramCount };
    var id = await repo.AddAsync(finalTemplate);
    return Results.Ok(new { id });
});

app.MapPost("/api/templates/{id}/test", async (int id, [Microsoft.AspNetCore.Mvc.FromBody] System.Text.Json.JsonElement body, ITemplateRepository templateRepo, IOutboxRepository outboxRepo, Microsoft.Extensions.Logging.ILogger<Program> logger) => 
{
    var phoneNumber = body.TryGetProperty("phoneNumber", out var p) ? p.GetString() : null;
    if (string.IsNullOrWhiteSpace(phoneNumber))
    {
        return Results.BadRequest(new { message = "Phone number is required." });
    }

    var template = await templateRepo.GetByIdAsync(id);
    if (template == null)
    {
        return Results.NotFound(new { message = "Template not found." });
    }

    var parameters = new List<string>();
    if (!string.IsNullOrEmpty(template.ParameterDefinitionsJson))
    {
        try
        {
            var defs = System.Text.Json.JsonSerializer.Deserialize<List<HotelAutomation.Core.Models.ParameterDefinition>>(template.ParameterDefinitionsJson, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (defs != null && defs.Count > 0)
            {
                parameters = defs
                    .OrderBy(d => d.Position)
                    .Select(d => !string.IsNullOrEmpty(d.SampleValue) ? d.SampleValue : $"Param{d.Position}")
                    .ToList();
            }
        }
        catch { }
    }

    if (parameters.Count == 0)
    {
        int count = template.BodyParamCount > 0 ? template.BodyParamCount : 0;
        if (count > 0)
        {
            var dummyData = new[] { "Test User", "101", "2500", "Sankalp Retreat", "9 Jul 2026", "12 Jul 2026", "TestGuest@2026", "BK-TEST-101" };
            for (int i = 0; i < count; i++)
            {
                parameters.Add(i < dummyData.Length ? dummyData[i] : $"TestParam{i + 1}");
            }
        }
    }

    var msg = new HotelAutomation.Core.Models.OutboxMessage
    {
        ToPhoneNumber = phoneNumber,
        TemplateId = template.Id,
        ParametersJson = System.Text.Json.JsonSerializer.Serialize(parameters)
    };
    
    try
    {
        await outboxRepo.AddAsync(msg);
        return Results.Ok(new { message = "Test message queued successfully!" });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to queue test message for template {TemplateId}", id);
        return Results.StatusCode(500);
    }
});

app.MapPut("/api/templates/{id}", async (int id, HotelAutomation.Core.Models.MessageTemplate template, ITemplateRepository repo) => 
{
    var trimmed = template with 
    { 
        Name = template.Name.Trim(), 
        ExternalTemplateName = template.ExternalTemplateName.Trim(), 
        LanguageCode = template.LanguageCode?.Trim() ?? "en" 
    };

    int paramCount = trimmed.BodyParamCount;
    if (!string.IsNullOrEmpty(trimmed.ParameterDefinitionsJson))
    {
        try
        {
            var defs = System.Text.Json.JsonSerializer.Deserialize<List<HotelAutomation.Core.Models.ParameterDefinition>>(trimmed.ParameterDefinitionsJson, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (defs != null)
            {
                paramCount = defs.Count;
            }
        }
        catch { }
    }

    var finalTemplate = trimmed with { BodyParamCount = paramCount };
    var success = await repo.UpdateAsync(id, finalTemplate);
    return success ? Results.Ok() : Results.NotFound();
});

app.MapDelete("/api/templates/{id}", async (int id, ITemplateRepository repo) => 
{
    try 
    {
        var success = await repo.DeleteAsync(id);
        return success ? Results.Ok(new { message = "Template deleted successfully" }) : Results.NotFound();
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
});

// ── Template Variants API (A/B Testing) ──────────────────────────────────────
app.MapGet("/api/templates/{templateId}/variants", async (int templateId, TemplateVariantRepository repo) => 
{
    return Results.Ok(await repo.GetByTemplateIdAsync(templateId));
});

app.MapPost("/api/templates/{templateId}/variants", async (int templateId, HotelAutomation.Core.Models.TemplateVariant variant, TemplateVariantRepository repo) => 
{
    if (string.IsNullOrWhiteSpace(variant.VariantName))
        return Results.BadRequest("VariantName is required.");
    if (string.IsNullOrWhiteSpace(variant.Body))
        return Results.BadRequest("Variant Body is required.");
    if (variant.Weight < 1)
        return Results.BadRequest("Weight must be at least 1.");

    var newVariant = variant with { TemplateId = templateId, CreatedAt = DateTimeOffset.UtcNow };
    await repo.AddAsync(newVariant);
    return Results.Ok(new { message = "Variant added." });
});

app.MapDelete("/api/templates/{templateId}/variants/{variantId}", async (int variantId, TemplateVariantRepository repo) => 
{
    await repo.DeleteAsync(variantId);
    return Results.Ok(new { message = "Variant deleted." });
});

// ── Template Variant Analytics ────────────────────────────────────────────────
app.MapGet("/api/templates/{templateId}/variants/analytics", async (int templateId, SqliteConnectionFactory dbFactory) => 
{
    await using var conn = await dbFactory.OpenAsync();
    var sql = @"
        SELECT 
            v.Id as VariantId, 
            v.VariantName, 
            COUNT(o.Id) as TotalSent,
            SUM(CASE WHEN o.Status = 'Sent' THEN 1 ELSE 0 END) as Delivered,
            SUM(CASE WHEN o.Status = 'Failed' THEN 1 ELSE 0 END) as Failed
        FROM TemplateVariants v
        LEFT JOIN OutboxMessages o ON o.VariantId = v.Id
        WHERE v.TemplateId = @templateId
        GROUP BY v.Id, v.VariantName";
    
    var results = await conn.QueryAsync(sql, new { templateId });
    return Results.Ok(results);
});

// ── Smart Lists API ──────────────────────────────────────────────────────────
app.MapGet("/api/smart-lists", async (ISmartListRepository repo) => 
{
    return await repo.GetAllAsync();
});

app.MapPost("/api/smart-lists", async (HotelAutomation.Core.Models.SmartList list, ISmartListRepository repo) => 
{
    if (string.IsNullOrWhiteSpace(list.ListName) || string.IsNullOrWhiteSpace(list.FilterConditionsJson))
        return Results.BadRequest(new { message = "List Name and Filters are required." });
        
    var id = await repo.AddAsync(list);
    return Results.Ok(new { Id = id });
});

app.MapDelete("/api/smart-lists/{id}", async (int id, ISmartListRepository repo) => 
{
    var success = await repo.DeleteAsync(id);
    return success ? Results.Ok() : Results.NotFound();
});

app.MapGet("/api/smart-lists/{id}/contacts", async (int id, ISmartListRepository listRepo, ContactRepository contactRepo, int page = 1, int limit = 50) => 
{
    var smartList = await listRepo.GetByIdAsync(id);
    if (smartList == null) return Results.NotFound();
    
    var (list, total) = await contactRepo.GetBySmartListAsync(smartList, page, limit);
    return Results.Ok(new { Contacts = list, TotalCount = total });
});

// ── Campaigns API ────────────────────────────────────────────────────────────
app.MapGet("/api/campaigns", async (ICampaignRepository repo) => await repo.GetAllAsync());

app.MapGet("/api/campaigns/{id}", async (int id, ICampaignRepository repo) => 
{
    var campaign = await repo.GetByIdAsync(id);
    return campaign != null ? Results.Ok(campaign) : Results.NotFound();
});

app.MapPost("/api/campaigns", async (HotelAutomation.Core.Models.Campaign campaign, ICampaignRepository repo) => 
{
    if (string.IsNullOrWhiteSpace(campaign.Name)) return Results.BadRequest("Name is required");
    var id = await repo.CreateAsync(campaign);
    return Results.Ok(new { Id = id });
});

app.MapPut("/api/campaigns/{id}", async (int id, HotelAutomation.Core.Models.Campaign campaign, ICampaignRepository repo) => 
{
    var success = await repo.UpdateAsync(id, campaign);
    return success ? Results.Ok() : Results.NotFound();
});

app.MapDelete("/api/campaigns/{id}", async (int id, ICampaignRepository repo) => 
{
    var success = await repo.DeleteAsync(id);
    return success ? Results.Ok() : Results.NotFound();
});

app.MapPost("/api/campaigns/{id}/enroll", async (int id, int smartListId, ICampaignRepository campRepo, ISmartListRepository slRepo, ContactRepository contactRepo, SqliteConnectionFactory factory) => 
{
    var campaign = await campRepo.GetByIdAsync(id);
    if (campaign == null || !campaign.Steps.Any()) return Results.BadRequest("Invalid campaign or no steps.");
    
    var smartList = await slRepo.GetByIdAsync(smartListId);
    if (smartList == null) return Results.BadRequest("Invalid Smart List.");
    
    // For large lists, we shouldn't fetch all into memory, but this is sufficient for MVP
    var (contacts, _) = await contactRepo.GetBySmartListAsync(smartList, 1, 10000);
    
    await using var conn = await factory.OpenAsync();
    using var transaction = conn.BeginTransaction();
    
    var firstStep = campaign.Steps.OrderBy(s => s.StepNumber).First();
    var nextTime = DateTimeOffset.UtcNow.AddMinutes(firstStep.DelayMinutes).ToString("O");
    
    var sql = @"
        INSERT OR IGNORE INTO ContactCampaignProgress (ContactId, CampaignStepId, Status, NextExecutionTime)
        VALUES (@ContactId, @CampaignStepId, 'Pending', @NextExecutionTime);
    ";
    
    await conn.ExecuteAsync(sql, contacts.Select(c => new
    {
        ContactId = c.Id,
        CampaignStepId = firstStep.Id,
        NextExecutionTime = nextTime
    }), transaction);
    
    transaction.Commit();
    return Results.Ok(new { EnrolledCount = contacts.Count });
});

app.MapPost("/api/rules", async (HotelAutomation.Core.Models.AutomationRule rule, SqliteConnectionFactory factory) => 
{
    using var conn = await factory.OpenAsync();
    var sql = @"INSERT INTO AutomationRules (Name, Description, TriggerEventType, TemplateId, IsEnabled, ConditionsJson, AudienceFilter, DelayMinutes) 
                VALUES (@Name, @Description, @TriggerEventType, @TemplateId, @IsEnabled, @ConditionsJson, @AudienceFilter, @DelayMinutes);
                SELECT last_insert_rowid();";
    var id = await conn.ExecuteScalarAsync<int>(sql, rule);
    return Results.Ok(new { id, rule.Name, rule.Description, rule.TriggerEventType, rule.TemplateId, rule.IsEnabled, rule.ConditionsJson, rule.AudienceFilter, rule.DelayMinutes });
});

app.MapPut("/api/rules/{id}", async (int id, HotelAutomation.Core.Models.AutomationRule rule, SqliteConnectionFactory factory) => 
{
    using var conn = await factory.OpenAsync();
    var sql = @"UPDATE AutomationRules 
                SET Name = @Name,
                    Description = @Description,
                    TriggerEventType = @TriggerEventType, 
                    TemplateId = @TemplateId, 
                    IsEnabled = @IsEnabled, 
                    ConditionsJson = @ConditionsJson, 
                    AudienceFilter = @AudienceFilter,
                    DelayMinutes = @DelayMinutes 
                WHERE Id = @Id";
    var rows = await conn.ExecuteAsync(sql, new { rule.Name, rule.Description, rule.TriggerEventType, rule.TemplateId, rule.IsEnabled, rule.ConditionsJson, rule.AudienceFilter, rule.DelayMinutes, Id = id });
    return rows > 0 ? Results.Ok(new { id }) : Results.NotFound();
});

app.MapPut("/api/rules/{id}/toggle", async (int id, SqliteConnectionFactory factory) => 
{
    using var conn = await factory.OpenAsync();
    var rule = await conn.QueryFirstOrDefaultAsync<HotelAutomation.Core.Models.AutomationRule>("SELECT * FROM AutomationRules WHERE Id = @Id", new { Id = id });
    if (rule == null) return Results.NotFound();
    
    var newStatus = !rule.IsEnabled;
    var rows = await conn.ExecuteAsync("UPDATE AutomationRules SET IsEnabled = @IsEnabled WHERE Id = @Id", new { IsEnabled = newStatus, Id = id });
    return rows > 0 ? Results.Ok() : Results.NotFound();
});

app.MapGet("/api/rules/{id}/stats", async (int id, SqliteConnectionFactory factory) => 
{
    using var conn = await factory.OpenAsync();
    var triggered = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM OutboxMessages WHERE RuleId = @Id", new { Id = id });
    var totalSent = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM OutboxMessages WHERE RuleId = @Id AND Status = 'Sent'", new { Id = id });
    var failed = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM OutboxMessages WHERE RuleId = @Id AND Status = 'Failed'", new { Id = id });
    var lastRunStr = await conn.ExecuteScalarAsync<string>("SELECT CreatedAt FROM OutboxMessages WHERE RuleId = @Id ORDER BY CreatedAt DESC LIMIT 1", new { Id = id });
    
    DateTime? lastRun = null;
    if (DateTime.TryParse(lastRunStr, out var parsed)) lastRun = parsed;

    return Results.Ok(new {
        Triggered = triggered,
        TotalSent = totalSent,
        Failed = failed,
        LastRun = lastRun
    });
});

app.MapGet("/api/rules/{id}/failures", async (int id, SqliteConnectionFactory factory) => 
{
    await using var conn = await factory.OpenAsync();
    var failures = await conn.QueryAsync(
        "SELECT ToPhoneNumber, FailureReason, CreatedAt FROM OutboxMessages WHERE RuleId = @id AND Status = 'Failed' ORDER BY CreatedAt DESC LIMIT 100", 
        new { id });
    return Results.Ok(failures);
});

app.MapGet("/api/rules/{id}/analytics-details", async (int id, SqliteConnectionFactory factory) => 
{
    await using var conn = await factory.OpenAsync();
    var details = await conn.QueryAsync(@"
        SELECT o.ToPhoneNumber, o.Status, o.FailureReason, o.CreatedAt, 
               CASE WHEN u.PhoneNumber IS NOT NULL THEN 1 ELSE 0 END as IsUnsubscribed 
        FROM OutboxMessages o
        LEFT JOIN UnsubscribedContacts u ON o.ToPhoneNumber = u.PhoneNumber
        WHERE o.RuleId = @id 
        ORDER BY o.CreatedAt DESC LIMIT 500", new { id });
    return Results.Ok(details);
});

app.MapGet("/api/broadcast/analytics-details", async (int templateId, string startedAt, SqliteConnectionFactory factory) => 
{
    await using var conn = await factory.OpenAsync();
    var details = await conn.QueryAsync(@"
        SELECT o.ToPhoneNumber, o.Status, o.FailureReason, o.CreatedAt,
               CASE WHEN u.PhoneNumber IS NOT NULL THEN 1 ELSE 0 END as IsUnsubscribed 
        FROM OutboxMessages o
        LEFT JOIN UnsubscribedContacts u ON o.ToPhoneNumber = u.PhoneNumber
        WHERE o.TemplateId = @TemplateId AND o.CreatedAt = @StartedAt 
        ORDER BY o.CreatedAt DESC LIMIT 500", new { TemplateId = templateId, StartedAt = startedAt });
    return Results.Ok(details);
});

app.MapDelete("/api/rules/{id}", async (int id, SqliteConnectionFactory factory) => 
{
    using var conn = await factory.OpenAsync();
    using var tx = conn.BeginTransaction();
    try 
    {
        await conn.ExecuteAsync("UPDATE OutboxMessages SET RuleId = NULL WHERE RuleId = @Id", new { Id = id }, tx);
        await conn.ExecuteAsync("UPDATE ScheduledJobs SET RuleId = NULL WHERE RuleId = @Id", new { Id = id }, tx);
        var rows = await conn.ExecuteAsync("DELETE FROM AutomationRules WHERE Id = @Id", new { Id = id }, tx);
        tx.Commit();
        return rows > 0 ? Results.Ok() : Results.NotFound();
    }
    catch 
    {
        tx.Rollback();
        throw;
    }
});

// ── Opt-Outs Endpoint ──────────────────────────────────────────────────────
app.MapGet("/api/unsubscribes", async (SqliteConnectionFactory factory) => 
{
    await using var conn = await factory.OpenAsync();
    var unsubscribes = await conn.QueryAsync(@"
        SELECT u.PhoneNumber, u.UnsubscribedAt, u.Source, c.FirstName, c.LastName 
        FROM UnsubscribedContacts u
        LEFT JOIN Contacts c ON u.PhoneNumber = c.PhoneNumber
        ORDER BY u.UnsubscribedAt DESC
    ");
    return Results.Ok(unsubscribes);
});

app.MapDelete("/api/unsubscribes/{phoneNumber}", async (string phoneNumber, UnsubscribedContactRepository repo) => 
{
    await repo.RemoveAsync(phoneNumber);
    return Results.Ok();
});

// ── Preference Center API ──────────────────────────────────────────────────
app.MapGet("/api/preferences/{contactId}", async (int contactId, ContactConsentRepository repo) => 
{
    var prefs = await repo.GetConsentPreferencesAsync(contactId);
    return Results.Ok(prefs);
});

app.MapPost("/api/preferences/{contactId}", async (int contactId, List<HotelAutomation.Core.Models.ContactPreferenceUpdate> updates, ContactConsentRepository repo) => 
{
    foreach (var update in updates)
    {
        await repo.UpsertConsentAsync(contactId, update.CategoryId, update.IsSubscribed);
    }
    return Results.Ok();
});

// ── Broadcast Campaign Endpoint ────────────────────────────────────────────
app.MapPost("/api/broadcast", async (BroadcastRequest req, BroadcastRepository broadcastRepo) =>
{
    if (req.TemplateId <= 0) return Results.BadRequest("TemplateId is required.");
    var count = await broadcastRepo.QueueBroadcastAsync(req.TemplateId, req.CategoryId, req.CustomParametersJson ?? "{}");
    return Results.Ok(new { queued = count, message = $"{count} messages queued for broadcast." });
});

app.MapGet("/api/broadcast", async (int? page, int? limit, BroadcastRepository broadcastRepo) =>
{
    var p = page ?? 1;
    var l = limit ?? 50;
    var (data, total) = await broadcastRepo.GetRecentBroadcastsPagedAsync(p, l);
    return Results.Ok(new { data, total });
});

app.MapDelete("/api/broadcast", async (int templateId, string startedAt, BroadcastRepository broadcastRepo) => 
{
    var stoppedCount = await broadcastRepo.StopBroadcastAsync(templateId, startedAt);
    return Results.Ok(new { stopped = stoppedCount, message = $"Stopped {stoppedCount} pending messages." });
});

app.MapDelete("/api/broadcast/history", async (int templateId, string startedAt, BroadcastRepository broadcastRepo) => 
{
    var deletedCount = await broadcastRepo.DeleteBroadcastAsync(templateId, startedAt);
    return Results.Ok(new { deleted = deletedCount, message = $"Deleted {deletedCount} messages from broadcast history." });
});

app.MapPost("/api/test-event", (string? phone, ManualPmsConnector pms) => 
{
    pms.RaiseTestEvent(new HotelAutomation.Core.Models.PmsEvent
    {
        EventType = "CheckOut",
        GuestName = "Test Guest",
        PhoneNumber = string.IsNullOrWhiteSpace(phone) ? "+1234567890" : phone,
        RoomNumber = "101",
        Amount = 150.00m,
        Timestamp = DateTimeOffset.UtcNow
    });
    return Results.Ok(new { message = "Test event fired successfully" });
});

app.MapGet("/api/inbox", async (InboxRepository repo) => 
{
    return await repo.GetRecentMessagesAsync();
});

// ── Two-Way Conversation Inbox Endpoints (Part B) ──

app.MapGet("/api/conversations", async (SqliteConnectionFactory dbFactory) =>
{
    using var conn = await dbFactory.OpenAsync();
    // Query active conversation threads based on numbers seen in InboxMessages or UnsubscribedContacts/OutboxMessages
    var threads = await conn.QueryAsync(@"
        SELECT DISTINCT 
            i.FromPhoneNumber as PhoneNumber, 
            c.FirstName, 
            c.LastName,
            (SELECT Content FROM InboxMessages WHERE FromPhoneNumber = i.FromPhoneNumber ORDER BY ReceivedAt DESC LIMIT 1) as LastMessageText,
            (SELECT ReceivedAt FROM InboxMessages WHERE FromPhoneNumber = i.FromPhoneNumber ORDER BY ReceivedAt DESC LIMIT 1) as LastMessageAt,
            (SELECT COUNT(*) FROM InboxMessages WHERE FromPhoneNumber = i.FromPhoneNumber AND IsRead = 0) as UnreadCount
        FROM InboxMessages i
        LEFT JOIN Contacts c ON i.FromPhoneNumber = c.PhoneNumber
        ORDER BY LastMessageAt DESC;
    ");
    return Results.Ok(threads);
});

app.MapGet("/api/conversations/{phoneNumber}/messages", async (string phoneNumber, SqliteConnectionFactory dbFactory) =>
{
    using var conn = await dbFactory.OpenAsync();
    
    // Mark incoming messages as read for this conversation
    await conn.ExecuteAsync("UPDATE InboxMessages SET IsRead = 1 WHERE FromPhoneNumber = @Phone", new { Phone = phoneNumber });

    // Fetch chronological thread combining guest messages and agent replies
    var messages = await conn.QueryAsync(@"
        SELECT 
            Id,
            FromPhoneNumber as PhoneNumber,
            'Guest' as Sender,
            Content,
            ReceivedAt as SentAt
        FROM InboxMessages
        WHERE FromPhoneNumber = @Phone
        
        UNION ALL
        
        SELECT 
            Id,
            PhoneNumber,
            Sender,
            Content,
            SentAt
        FROM ConversationReplies
        WHERE PhoneNumber = @Phone
        
        ORDER BY SentAt ASC;
    ", new { Phone = phoneNumber });

    return Results.Ok(messages);
});

app.MapPost("/api/conversations/{phoneNumber}/reply", async (string phoneNumber, [Microsoft.AspNetCore.Mvc.FromBody] WebhookPayload body, SqliteConnectionFactory dbFactory, WhatsAppApiClient apiClient, Microsoft.Extensions.Logging.ILogger<Program> logger) =>
{
    if (string.IsNullOrWhiteSpace(body.Body))
    {
        return Results.BadRequest(new { message = "Reply content cannot be empty." });
    }

    using var conn = await dbFactory.OpenAsync();

    // Verify 24-hour service window from Meta. Check guest's last message time
    var lastReceived = await conn.QueryFirstOrDefaultAsync<string>(
        "SELECT ReceivedAt FROM InboxMessages WHERE FromPhoneNumber = @Phone ORDER BY ReceivedAt DESC LIMIT 1",
        new { Phone = phoneNumber }
    );

    if (string.IsNullOrEmpty(lastReceived))
    {
        return Results.BadRequest(new { message = "Cannot initiate a conversation without prior incoming WhatsApp message from the recipient." });
    }

    if (DateTimeOffset.TryParse(lastReceived, out var lastReceivedTime))
    {
        var age = DateTimeOffset.UtcNow - lastReceivedTime;
        if (age.TotalHours > 24)
        {
            return Results.BadRequest(new { message = "The 24-hour customer service window has expired. You must use an approved Meta Message Template to resume contact." });
        }
    }

    // Build free-form text payload for WhatsApp Cloud API
    var payload = new
    {
        messaging_product = "whatsapp",
        recipient_type = "individual",
        to = phoneNumber,
        type = "text",
        text = new
        {
            preview_url = false,
            body = body.Body
        }
    };

    var (success, errorBody) = await apiClient.PostAsync(payload);
    if (!success)
    {
        logger.LogError("Failed to send free-form message reply to {Phone}. Meta returned: {Error}", phoneNumber, errorBody);
        return Results.BadRequest(new { message = $"Meta API Rejected Reply: {errorBody ?? "Unknown Error"}" });
    }

    // Parse external message ID if present
    string? externalId = null;
    try
    {
        using var doc = System.Text.Json.JsonDocument.Parse(errorBody ?? "{}");
        if (doc.RootElement.TryGetProperty("messages", out var messagesArray) &&
            messagesArray.GetArrayLength() > 0 &&
            messagesArray[0].TryGetProperty("id", out var idProp))
        {
            externalId = idProp.GetString();
        }
    }
    catch { }

    // Save reply history to ConversationReplies
    var replyId = externalId ?? Guid.NewGuid().ToString();
    await conn.ExecuteAsync(@"
        INSERT INTO ConversationReplies (Id, PhoneNumber, Sender, Content, SentAt)
        VALUES (@Id, @Phone, 'Agent', @Content, @SentAt);",
        new
        {
            Id = replyId,
            Phone = phoneNumber,
            Content = body.Body,
            SentAt = DateTimeOffset.UtcNow.ToString("O")
        });

    return Results.Ok(new { message = "Reply sent successfully!", id = replyId });
});

// ── Analytics Overview Endpoints (Part B) ──

app.MapGet("/api/analytics/overview", async (string? startDate, string? endDate, SqliteConnectionFactory dbFactory) =>
{
    using var conn = await dbFactory.OpenAsync();
    
    string dateFilter = "";
    var parameters = new DynamicParameters();
    
    if (!string.IsNullOrEmpty(startDate) && !string.IsNullOrEmpty(endDate))
    {
        // Add time boundaries if they are just dates
        var start = startDate.Contains("T") ? startDate : startDate + "T00:00:00Z";
        var end = endDate.Contains("T") ? endDate : endDate + "T23:59:59Z";
        dateFilter = "WHERE CreatedAt >= @start AND CreatedAt <= @end";
        parameters.Add("start", start);
        parameters.Add("end", end);
    }

    var totalTriggered = await conn.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM OutboxMessages {dateFilter}", parameters);
    var sent = await conn.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM OutboxMessages WHERE Status = 'Sent' {(string.IsNullOrEmpty(dateFilter) ? "" : "AND CreatedAt >= @start AND CreatedAt <= @end")}", parameters);
    var failed = await conn.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM OutboxMessages WHERE Status = 'Failed' {(string.IsNullOrEmpty(dateFilter) ? "" : "AND CreatedAt >= @start AND CreatedAt <= @end")}", parameters);
    var pending = await conn.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM OutboxMessages WHERE Status = 'Pending' {(string.IsNullOrEmpty(dateFilter) ? "" : "AND CreatedAt >= @start AND CreatedAt <= @end")}", parameters);
    var delivered = await conn.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM OutboxMessages WHERE Status = 'Delivered' {(string.IsNullOrEmpty(dateFilter) ? "" : "AND CreatedAt >= @start AND CreatedAt <= @end")}", parameters);
    var read = await conn.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM OutboxMessages WHERE Status = 'Read' {(string.IsNullOrEmpty(dateFilter) ? "" : "AND CreatedAt >= @start AND CreatedAt <= @end")}", parameters);
    var clicked = await conn.ExecuteScalarAsync<int>($"SELECT COUNT(DISTINCT o.Id) FROM OutboxMessages o INNER JOIN ShortenedLinks s ON o.Id = s.OutboxMessageId WHERE s.ClickedCount > 0 {(string.IsNullOrEmpty(dateFilter) ? "" : "AND o.CreatedAt >= @start AND o.CreatedAt <= @end")}", parameters);
    
    // Fallback logic for inbox messages (ReceivedAt)
    string inboxDateFilter = "";
    if (!string.IsNullOrEmpty(startDate) && !string.IsNullOrEmpty(endDate))
    {
        inboxDateFilter = "WHERE ReceivedAt >= @start AND ReceivedAt <= @end";
    }
    var totalInbox = await conn.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM InboxMessages {inboxDateFilter}", parameters);

    return Results.Ok(new
    {
        TotalTriggered = totalTriggered,
        Sent = sent,
        Delivered = delivered,
        Read = read,
        Failed = failed,
        Pending = pending,
        IncomingReplies = totalInbox,
        Clicked = clicked
    });
});

app.MapGet("/api/analytics/delivery-trends", async (string? startDate, string? endDate, SqliteConnectionFactory dbFactory) =>
{
    using var conn = await dbFactory.OpenAsync();
    
    string dateFilter = "WHERE CreatedAt >= date('now', '-7 days')";
    var parameters = new DynamicParameters();

    if (!string.IsNullOrEmpty(startDate) && !string.IsNullOrEmpty(endDate))
    {
        var start = startDate.Contains("T") ? startDate : startDate + "T00:00:00Z";
        var end = endDate.Contains("T") ? endDate : endDate + "T23:59:59Z";
        dateFilter = "WHERE CreatedAt >= @start AND CreatedAt <= @end";
        parameters.Add("start", start);
        parameters.Add("end", end);
    }

    var trends = await conn.QueryAsync($@"
        SELECT 
            SUBSTR(CreatedAt, 1, 10) as Day,
            SUM(CASE WHEN Status IN ('Sent', 'Delivered', 'Read') THEN 1 ELSE 0 END) as Success,
            SUM(CASE WHEN Status = 'Failed' THEN 1 ELSE 0 END) as Failed
        FROM OutboxMessages
        {dateFilter}
        GROUP BY Day
        ORDER BY Day ASC;
    ", parameters);
    return Results.Ok(trends);
});

app.MapGet("/api/l/{shortCode}", async (string shortCode, SqliteConnectionFactory dbFactory) =>
{
    using var conn = await dbFactory.OpenAsync();
    var link = await conn.QueryFirstOrDefaultAsync("SELECT OriginalUrl, OutboxMessageId FROM ShortenedLinks WHERE ShortCode = @shortCode", new { shortCode });
    if (link == null)
    {
        return Results.NotFound("Link not found.");
    }

    // Record the click asynchronously in the background so we don't slow down redirect
    _ = Task.Run(async () =>
    {
        try
        {
            using var bgConn = await dbFactory.OpenAsync();
            var now = DateTimeOffset.UtcNow.ToString("O");
            await bgConn.ExecuteAsync(@"
                UPDATE ShortenedLinks 
                SET ClickedCount = ClickedCount + 1,
                    FirstClickedAt = COALESCE(FirstClickedAt, @now),
                    LastClickedAt = @now
                WHERE ShortCode = @shortCode", new { now, shortCode });
            
            // Optionally, we could update the OutboxMessage status to Clicked here, 
            // but reading the ShortenedLinks table is safer and preserves the Read status timeline.
        }
        catch { }
    });

    return Results.Redirect((string)link.OriginalUrl, permanent: false);
});

app.MapGet("/api/analytics/export", async (SqliteConnectionFactory dbFactory) =>
{
    using var conn = await dbFactory.OpenAsync();
    var messages = await conn.QueryAsync(@"
        SELECT 
            o.Id, 
            o.ToPhoneNumber, 
            t.Name as TemplateName, 
            o.Status, 
            o.FailureReason, 
            o.CreatedAt 
        FROM OutboxMessages o
        LEFT JOIN MessageTemplates t ON o.TemplateId = t.Id
        ORDER BY o.CreatedAt DESC;
    ");

    var sb = new System.Text.StringBuilder();
    sb.AppendLine("MessageId,Recipient,Template,Status,FailureReason,Timestamp");
    foreach (var m in messages)
    {
        var reason = (m.FailureReason ?? "").Replace(",", " ");
        sb.AppendLine($"{m.Id},{m.ToPhoneNumber},{m.TemplateName},{m.Status},{reason},{m.CreatedAt}");
    }

    return Results.Bytes(System.Text.Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", "MessageReport.csv");
});

app.MapGet("/api/webhooks/whatsapp", (HttpContext ctx, Microsoft.Extensions.Options.IOptions<WhatsAppOptions> options) => 
{
    var mode = ctx.Request.Query["hub.mode"].ToString();
    var verifyToken = ctx.Request.Query["hub.verify_token"].ToString();
    var challenge = ctx.Request.Query["hub.challenge"].ToString();

    var expectedToken = options.Value.VerifyToken;
    if (string.IsNullOrEmpty(expectedToken))
    {
        expectedToken = "sankalp_retreat_verify_token"; // Default fallback
    }

    if (mode == "subscribe" && verifyToken == expectedToken)
    {
        return Results.Text(challenge, "text/plain");
    }

    return Results.BadRequest("Verification failed");
});

app.MapPost("/api/webhooks/whatsapp", async (HttpContext ctx, InboxRepository inboxRepo, JobRepository jobRepo, SqliteConnectionFactory dbFactory, UnsubscribedContactRepository unsubRepo) => 
{
    using var reader = new StreamReader(ctx.Request.Body);
    var body = await reader.ReadToEndAsync();
    
    try
    {
        using var doc = System.Text.Json.JsonDocument.Parse(body);
        var entryArray = doc.RootElement.GetProperty("entry");
        foreach (var entry in entryArray.EnumerateArray())
        {
            var changesArray = entry.GetProperty("changes");
            foreach (var change in changesArray.EnumerateArray())
            {
                var value = change.GetProperty("value");
                
                // Handle inbound messages (Replies)
                if (value.TryGetProperty("messages", out var messages))
                {
                    foreach (var msg in messages.EnumerateArray())
                    {
                        var from = msg.GetProperty("from").GetString();
                        var text = msg.GetProperty("text").GetProperty("body").GetString();
                        
                        var inboxMsg = new HotelAutomation.Core.Models.InboxMessage
                        {
                            FromPhoneNumber = from ?? "unknown",
                            Content = text ?? "",
                            IsRead = false,
                            ReceivedAt = DateTimeOffset.UtcNow
                        };
                        await inboxRepo.AddAsync(inboxMsg);
                        await jobRepo.CancelJobsForPhoneNumberAsync(from ?? "");
                        if (!string.IsNullOrEmpty(from) && !string.IsNullOrEmpty(text) && text.Trim().Equals("STOP", StringComparison.OrdinalIgnoreCase))
                        {
                            await unsubRepo.AddAsync(from, "STOP_Keyword");
                        }
                    }
                }
                
                // Handle user_preferences (Marketing opt-out toggles)
                if (value.TryGetProperty("user_preferences", out var userPreferences))
                {
                    foreach (var pref in userPreferences.EnumerateArray())
                    {
                        var from = pref.GetProperty("wa_id").GetString();
                        var prefValue = pref.GetProperty("value").GetString();
                        
                        if (!string.IsNullOrEmpty(from))
                        {
                            if (prefValue == "stop")
                                await unsubRepo.AddAsync(from, "WhatsApp_Native");
                            else if (prefValue == "resume")
                                await unsubRepo.RemoveAsync(from);
                        }
                    }
                }
                
                // Handle statuses (Delivered, Read)
                if (value.TryGetProperty("statuses", out var statuses))
                {
                    foreach (var status in statuses.EnumerateArray())
                    {
                        var id = status.GetProperty("id").GetString();
                        var statusStr = status.GetProperty("status").GetString(); // e.g., 'delivered', 'read'
                        
                        if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(statusStr))
                        {
                            // Update our DB mapping
                            using var conn = await dbFactory.OpenAsync();
                            // Only update if it's progressing forward (e.g. Sent -> Delivered -> Read)
                            // For simplicity, we just overwrite if it's Delivered or Read.
                            var capitalizedStatus = char.ToUpper(statusStr[0]) + statusStr.Substring(1);
                            await conn.ExecuteAsync("UPDATE OutboxMessages SET Status = @Status WHERE ExternalMessageId = @Id", new { Status = capitalizedStatus, Id = id });
                        }
                    }
                }
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Webhook parsing failed: {ex.Message}");
    }

    return Results.Ok();
});

app.MapPost("/api/manual/checkin", async (HotelAutomation.Core.Models.PmsEventPayload payload, ManualPmsConnector pms) => 
{
    pms.RaiseTestEvent(new HotelAutomation.Core.Models.PmsEvent
    {
        EventType = "PMS_CHECKIN",
        GuestName = payload.GuestName,
        PhoneNumber = payload.PhoneNumber,
        RoomNumber = payload.RoomNumber,
        Amount = payload.TotalBill,
        CheckInDate = payload.CheckInDate ?? DateTimeOffset.UtcNow.ToString("d MMM yyyy"),
        CheckOutDate = payload.CheckOutDate,
        WifiPassword = payload.WifiPassword,
        BookingReference = payload.BookingReference,
        GuestFeedbackLink = payload.GuestFeedbackLink,
        HotelName = payload.HotelName,
        ExtraNote = payload.ExtraNote,
        Timestamp = DateTimeOffset.UtcNow,
        RawPayload = System.Text.Json.JsonSerializer.Serialize(payload)
    });
    return Results.Ok(new { status = "success", message = "Manual Check-In triggered successfully." });
});

app.MapPost("/api/manual/checkout", async (HotelAutomation.Core.Models.PmsEventPayload payload, ManualPmsConnector pms) => 
{
    pms.RaiseTestEvent(new HotelAutomation.Core.Models.PmsEvent
    {
        EventType = "PMS_CHECKOUT",
        GuestName = payload.GuestName,
        PhoneNumber = payload.PhoneNumber,
        RoomNumber = payload.RoomNumber,
        Amount = payload.TotalBill,
        CheckInDate = payload.CheckInDate,
        CheckOutDate = payload.CheckOutDate ?? DateTimeOffset.UtcNow.ToString("d MMM yyyy"),
        WifiPassword = payload.WifiPassword,
        BookingReference = payload.BookingReference,
        GuestFeedbackLink = payload.GuestFeedbackLink,
        HotelName = payload.HotelName,
        ExtraNote = payload.ExtraNote,
        Timestamp = DateTimeOffset.UtcNow,
        RawPayload = System.Text.Json.JsonSerializer.Serialize(payload)
    });
    return Results.Ok(new { status = "success", message = "Manual Check-Out triggered successfully." });
});

app.MapPost("/api/pms/checkin", async (HotelAutomation.Core.Models.PmsEventPayload payload, ManualPmsConnector pms) => 
{
    pms.RaiseTestEvent(new HotelAutomation.Core.Models.PmsEvent
    {
        EventType = "PMS_CHECKIN",
        GuestName = payload.GuestName,
        PhoneNumber = payload.PhoneNumber,
        RoomNumber = payload.RoomNumber,
        Amount = payload.TotalBill,
        CheckInDate = payload.CheckInDate ?? DateTimeOffset.UtcNow.ToString("d MMM yyyy"),
        CheckOutDate = payload.CheckOutDate,
        WifiPassword = payload.WifiPassword,
        BookingReference = payload.BookingReference,
        GuestFeedbackLink = payload.GuestFeedbackLink,
        HotelName = payload.HotelName,
        ExtraNote = payload.ExtraNote,
        Timestamp = DateTimeOffset.UtcNow,
        RawPayload = System.Text.Json.JsonSerializer.Serialize(payload)
    });
    return Results.Ok(new { status = "success", message = "CheckIn event queued." });
}).AddEndpointFilter(async (ctx, next) =>
{
    if (!ctx.HttpContext.Request.Headers.TryGetValue("X-API-Key", out var extractedApiKey)) return Results.Unauthorized();
    if (extractedApiKey != "SUPER_SECRET_SCALIFTS_KEY_2026") return Results.Unauthorized();
    return await next(ctx);
});

app.MapPost("/api/pms/checkout", async (HotelAutomation.Core.Models.PmsEventPayload payload, ManualPmsConnector pms) => 
{
    pms.RaiseTestEvent(new HotelAutomation.Core.Models.PmsEvent
    {
        EventType = "PMS_CHECKOUT",
        GuestName = payload.GuestName,
        PhoneNumber = payload.PhoneNumber,
        RoomNumber = payload.RoomNumber,
        Amount = payload.TotalBill,
        CheckInDate = payload.CheckInDate,
        CheckOutDate = payload.CheckOutDate ?? DateTimeOffset.UtcNow.ToString("d MMM yyyy"),
        WifiPassword = payload.WifiPassword,
        BookingReference = payload.BookingReference,
        GuestFeedbackLink = payload.GuestFeedbackLink,
        HotelName = payload.HotelName,
        ExtraNote = payload.ExtraNote,
        Timestamp = DateTimeOffset.UtcNow,
        RawPayload = System.Text.Json.JsonSerializer.Serialize(payload)
    });
    return Results.Ok(new { status = "success", message = "CheckOut event queued." });
}).AddEndpointFilter(async (ctx, next) =>
{
    if (!ctx.HttpContext.Request.Headers.TryGetValue("X-API-Key", out var extractedApiKey)) return Results.Unauthorized();
    if (extractedApiKey != "SUPER_SECRET_SCALIFTS_KEY_2026") return Results.Unauthorized();
    return await next(ctx);
});

app.MapPost("/api/pms/booking-created", async (HotelAutomation.Core.Models.PmsEventPayload payload, ManualPmsConnector pms) => 
{
    pms.RaiseTestEvent(new HotelAutomation.Core.Models.PmsEvent
    {
        EventType = "BOOKING_CREATED",
        GuestName = payload.GuestName,
        PhoneNumber = payload.PhoneNumber,
        RoomNumber = payload.RoomNumber,
        Amount = payload.TotalBill,
        CheckInDate = payload.CheckInDate,
        CheckOutDate = payload.CheckOutDate,
        WifiPassword = payload.WifiPassword,
        BookingReference = payload.BookingReference,
        GuestFeedbackLink = payload.GuestFeedbackLink,
        HotelName = payload.HotelName,
        ExtraNote = payload.ExtraNote,
        Timestamp = DateTimeOffset.UtcNow,
        RawPayload = System.Text.Json.JsonSerializer.Serialize(payload)
    });
    return Results.Ok(new { status = "success", message = "Booking Created event queued." });
}).AddEndpointFilter(async (ctx, next) =>
{
    if (!ctx.HttpContext.Request.Headers.TryGetValue("X-API-Key", out var extractedApiKey)) return Results.Unauthorized();
    if (extractedApiKey != "SUPER_SECRET_SCALIFTS_KEY_2026") return Results.Unauthorized();
    return await next(ctx);
});

// ── Booking Modified ──────────────────────────────────────────────────────────
app.MapPost("/api/pms/booking-modified", async (HotelAutomation.Core.Models.PmsEventPayload payload, ManualPmsConnector pms) => 
{
    pms.RaiseTestEvent(new HotelAutomation.Core.Models.PmsEvent
    {
        EventType = "BOOKING_MODIFIED",
        GuestName = payload.GuestName,
        PhoneNumber = payload.PhoneNumber,
        RoomNumber = payload.RoomNumber,
        Amount = payload.TotalBill,
        CheckInDate = payload.CheckInDate,
        CheckOutDate = payload.CheckOutDate,
        WifiPassword = payload.WifiPassword,
        BookingReference = payload.BookingReference,
        GuestFeedbackLink = payload.GuestFeedbackLink,
        HotelName = payload.HotelName,
        ExtraNote = payload.ExtraNote,
        Timestamp = DateTimeOffset.UtcNow,
        RawPayload = System.Text.Json.JsonSerializer.Serialize(payload)
    });
    return Results.Ok(new { status = "success", message = "Booking Modified event queued." });
}).AddEndpointFilter(async (ctx, next) =>
{
    if (!ctx.HttpContext.Request.Headers.TryGetValue("X-API-Key", out var extractedApiKey)) return Results.Unauthorized();
    if (extractedApiKey != "SUPER_SECRET_SCALIFTS_KEY_2026") return Results.Unauthorized();
    return await next(ctx);
});

// ── Booking Cancelled ─────────────────────────────────────────────────────────
app.MapPost("/api/pms/booking-cancelled", async (HotelAutomation.Core.Models.PmsEventPayload payload, ManualPmsConnector pms) => 
{
    pms.RaiseTestEvent(new HotelAutomation.Core.Models.PmsEvent
    {
        EventType = "BOOKING_CANCELLED",
        GuestName = payload.GuestName,
        PhoneNumber = payload.PhoneNumber,
        RoomNumber = payload.RoomNumber,
        Amount = payload.TotalBill,
        CheckInDate = payload.CheckInDate,
        CheckOutDate = payload.CheckOutDate,
        WifiPassword = payload.WifiPassword,
        BookingReference = payload.BookingReference,
        GuestFeedbackLink = payload.GuestFeedbackLink,
        HotelName = payload.HotelName,
        ExtraNote = payload.ExtraNote,
        Timestamp = DateTimeOffset.UtcNow,
        RawPayload = System.Text.Json.JsonSerializer.Serialize(payload)
    });
    return Results.Ok(new { status = "success", message = "Booking Cancelled event queued." });
}).AddEndpointFilter(async (ctx, next) =>
{
    if (!ctx.HttpContext.Request.Headers.TryGetValue("X-API-Key", out var extractedApiKey)) return Results.Unauthorized();
    if (extractedApiKey != "SUPER_SECRET_SCALIFTS_KEY_2026") return Results.Unauthorized();
    return await next(ctx);
});

// ── Room Service Request ───────────────────────────────────────────────────────
app.MapPost("/api/pms/room-service-request", async (HotelAutomation.Core.Models.PmsEventPayload payload, ManualPmsConnector pms) => 
{
    pms.RaiseTestEvent(new HotelAutomation.Core.Models.PmsEvent
    {
        EventType = "ROOM_SERVICE_REQUEST",
        GuestName = payload.GuestName,
        PhoneNumber = payload.PhoneNumber,
        RoomNumber = payload.RoomNumber,
        Amount = payload.TotalBill,
        CheckInDate = payload.CheckInDate,
        CheckOutDate = payload.CheckOutDate,
        HotelName = payload.HotelName,
        ExtraNote = payload.ExtraNote,
        Timestamp = DateTimeOffset.UtcNow,
        RawPayload = System.Text.Json.JsonSerializer.Serialize(payload)
    });
    return Results.Ok(new { status = "success", message = "Room Service Request event queued." });
}).AddEndpointFilter(async (ctx, next) =>
{
    if (!ctx.HttpContext.Request.Headers.TryGetValue("X-API-Key", out var extractedApiKey)) return Results.Unauthorized();
    if (extractedApiKey != "SUPER_SECRET_SCALIFTS_KEY_2026") return Results.Unauthorized();
    return await next(ctx);
});

// ── Payment Received ──────────────────────────────────────────────────────────
app.MapPost("/api/pms/payment-received", async (HotelAutomation.Core.Models.PmsEventPayload payload, ManualPmsConnector pms) => 
{
    pms.RaiseTestEvent(new HotelAutomation.Core.Models.PmsEvent
    {
        EventType = "PAYMENT_RECEIVED",
        GuestName = payload.GuestName,
        PhoneNumber = payload.PhoneNumber,
        RoomNumber = payload.RoomNumber,
        Amount = payload.TotalBill,
        BookingReference = payload.BookingReference,
        HotelName = payload.HotelName,
        ExtraNote = payload.ExtraNote,
        Timestamp = DateTimeOffset.UtcNow,
        RawPayload = System.Text.Json.JsonSerializer.Serialize(payload)
    });
    return Results.Ok(new { status = "success", message = "Payment Received event queued." });
}).AddEndpointFilter(async (ctx, next) =>
{
    if (!ctx.HttpContext.Request.Headers.TryGetValue("X-API-Key", out var extractedApiKey)) return Results.Unauthorized();
    if (extractedApiKey != "SUPER_SECRET_SCALIFTS_KEY_2026") return Results.Unauthorized();
    return await next(ctx);
});

// ── Complaint Raised ──────────────────────────────────────────────────────────
app.MapPost("/api/pms/complaint-raised", async (HotelAutomation.Core.Models.PmsEventPayload payload, ManualPmsConnector pms) => 
{
    pms.RaiseTestEvent(new HotelAutomation.Core.Models.PmsEvent
    {
        EventType = "COMPLAINT_RAISED",
        GuestName = payload.GuestName,
        PhoneNumber = payload.PhoneNumber,
        RoomNumber = payload.RoomNumber,
        HotelName = payload.HotelName,
        ExtraNote = payload.ExtraNote,
        Timestamp = DateTimeOffset.UtcNow,
        RawPayload = System.Text.Json.JsonSerializer.Serialize(payload)
    });
    return Results.Ok(new { status = "success", message = "Complaint Raised event queued." });
}).AddEndpointFilter(async (ctx, next) =>
{
    if (!ctx.HttpContext.Request.Headers.TryGetValue("X-API-Key", out var extractedApiKey)) return Results.Unauthorized();
    if (extractedApiKey != "SUPER_SECRET_SCALIFTS_KEY_2026") return Results.Unauthorized();
    return await next(ctx);
});

// ── Event / API Variable Trigger ──────────────────────────────────────────────
// POST /api/event-trigger/{slug}
// Any external system can call this with a JSON body containing arbitrary key-value pairs.
// The system will find all automation rules of type EVENT_API whose ConditionsJson contains
// a matching EventApiSlug, and fire them with the body fields merged as dynamic parameters.
app.MapPost("/api/event-trigger/{slug}", async (
    string slug,
    HttpContext ctx,
    SqliteConnectionFactory dbFactory,
    ManualPmsConnector pms,
    HotelAutomation.Data.Repositories.ContactRepository contactRepo,
    Microsoft.Extensions.Logging.ILogger<Program> logger) =>
{
    if (string.IsNullOrWhiteSpace(slug))
        return Results.BadRequest(new { error = "Slug is required." });

    using var reader = new System.IO.StreamReader(ctx.Request.Body);
    var body = await reader.ReadToEndAsync();

    // Validate incoming JSON
    Dictionary<string, string>? incomingFields = null;
    try
    {
        using var doc = System.Text.Json.JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
        incomingFields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            incomingFields[prop.Name] = prop.Value.ToString();
        }
    }
    catch
    {
        return Results.BadRequest(new { error = "Invalid JSON body." });
    }

    // Extract required fields: phone number and guest name are mandatory
    if (!incomingFields.TryGetValue("phone", out var phone) && !incomingFields.TryGetValue("PhoneNumber", out phone))
        return Results.BadRequest(new { error = "A 'phone' or 'PhoneNumber' field is required in the request body." });

    if (!incomingFields.TryGetValue("name", out var guestName) && !incomingFields.TryGetValue("GuestName", out guestName))
        guestName = "Guest";

    // Upsert contact with payload data (only saving approved/existing columns)
    try
    {
        var existingColumns = new HashSet<string>(await contactRepo.GetDynamicColumnsAsync(), StringComparer.OrdinalIgnoreCase);
        
        var firstName = incomingFields.TryGetValue("first_name", out var fn) ? fn : 
                        incomingFields.TryGetValue("firstname", out var fn2) ? fn2 : 
                        incomingFields.TryGetValue("name", out var n) ? n.Split(' ')[0] : guestName.Split(' ')[0];
        
        var lastName = incomingFields.TryGetValue("last_name", out var ln) ? ln : 
                       incomingFields.TryGetValue("lastname", out var ln2) ? ln2 : 
                       incomingFields.TryGetValue("name", out var n2) && n2.Contains(' ') ? n2.Substring(n2.IndexOf(' ') + 1) : 
                       guestName.Contains(' ') ? guestName.Substring(guestName.IndexOf(' ') + 1) : "";

        var customFields = new Dictionary<string, object>();
        foreach (var kv in incomingFields)
        {
            if (kv.Key.Equals("phone", StringComparison.OrdinalIgnoreCase) || 
                kv.Key.Equals("PhoneNumber", StringComparison.OrdinalIgnoreCase) ||
                kv.Key.Equals("name", StringComparison.OrdinalIgnoreCase) ||
                kv.Key.Equals("first_name", StringComparison.OrdinalIgnoreCase) ||
                kv.Key.Equals("last_name", StringComparison.OrdinalIgnoreCase) ||
                kv.Key.Equals("firstname", StringComparison.OrdinalIgnoreCase) ||
                kv.Key.Equals("lastname", StringComparison.OrdinalIgnoreCase) ||
                kv.Key.Equals("GuestName", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (existingColumns.Contains(kv.Key))
            {
                customFields[kv.Key] = kv.Value;
            }
        }

        var existingContact = await contactRepo.GetByPhoneNumberAsync(phone);
        if (existingContact != null)
        {
            var existingCustomFields = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(existingContact.CustomFieldsJson ?? "{}") ?? new Dictionary<string, object>();
            foreach (var kv in customFields)
            {
                existingCustomFields[kv.Key] = kv.Value;
            }
            
            var updatedContact = new HotelAutomation.Core.Models.Contact
            {
                Id = existingContact.Id,
                PhoneNumber = existingContact.PhoneNumber,
                FirstName = !string.IsNullOrWhiteSpace(firstName) ? firstName : existingContact.FirstName,
                LastName = !string.IsNullOrWhiteSpace(lastName) ? lastName : existingContact.LastName,
                CategoryId = existingContact.CategoryId,
                CustomFieldsJson = System.Text.Json.JsonSerializer.Serialize(existingCustomFields),
                CreatedAt = existingContact.CreatedAt,
                UpdatedAt = DateTimeOffset.UtcNow,
                UpdateCount = existingContact.UpdateCount,
                CheckInCount = existingContact.CheckInCount,
                AverageStayDays = existingContact.AverageStayDays,
                IsVip = existingContact.IsVip
            };
            await contactRepo.UpdateAsync(existingContact.Id, updatedContact);
        }
        else
        {
            var newContact = new HotelAutomation.Core.Models.Contact
            {
                PhoneNumber = phone,
                FirstName = firstName,
                LastName = lastName,
                CustomFieldsJson = System.Text.Json.JsonSerializer.Serialize(customFields),
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            await contactRepo.InsertAsync(newContact);
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to upsert contact for event-trigger payload.");
    }

    // Find all EVENT_API rules whose ConditionsJson contains this slug
    using var conn = await dbFactory.OpenAsync();
    var eventApiRules = await conn.QueryAsync<HotelAutomation.Core.Models.AutomationRule>(
        "SELECT * FROM AutomationRules WHERE TriggerEventType = 'EVENT_API' AND IsEnabled = 1");

    int firedCount = 0;
    foreach (var rule in eventApiRules)
    {
        if (string.IsNullOrWhiteSpace(rule.ConditionsJson)) continue;
        try
        {
            var cond = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(rule.ConditionsJson);
            if (!cond.TryGetProperty("EventApiSlug", out var slugProp)) continue;
            if (!string.Equals(slugProp.GetString(), slug, StringComparison.OrdinalIgnoreCase)) continue;

            // Apply optional variable mappings defined in the rule
            var renamedFields = new Dictionary<string, string>(incomingFields, StringComparer.OrdinalIgnoreCase);
            if (cond.TryGetProperty("VarMappings", out var varMappings) && varMappings.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var mapping in varMappings.EnumerateArray())
                {
                    if (mapping.TryGetProperty("jsonKey", out var jk) && mapping.TryGetProperty("templateParam", out var tp))
                    {
                        var jsonKey = jk.GetString() ?? "";
                        var templateParam = tp.GetString() ?? "";
                        if (!string.IsNullOrWhiteSpace(jsonKey) && !string.IsNullOrWhiteSpace(templateParam)
                            && renamedFields.TryGetValue(jsonKey, out var val))
                        {
                            renamedFields[templateParam] = val;
                        }
                    }
                }
            }

            // Build raw payload from the (possibly remapped) fields
            var rawPayload = System.Text.Json.JsonSerializer.Serialize(renamedFields);

            // Fire the event through the existing PMS connector so HotelWorker picks it up
            pms.RaiseTestEvent(new HotelAutomation.Core.Models.PmsEvent
            {
                EventType = "EVENT_API",
                GuestName = guestName,
                PhoneNumber = phone,
                RoomNumber = renamedFields.TryGetValue("room", out var room) ? room :
                             renamedFields.TryGetValue("RoomNumber", out var roomNum) ? roomNum : "",
                HotelName = renamedFields.TryGetValue("hotelName", out var hn) ? hn :
                            renamedFields.TryGetValue("HotelName", out var hn2) ? hn2 : "Sankalp Retreat",
                ExtraNote = renamedFields.TryGetValue("note", out var note) ? note :
                            renamedFields.TryGetValue("ExtraNote", out var en) ? en : "",
                Timestamp = DateTimeOffset.UtcNow,
                RawPayload = rawPayload
            });
            firedCount++;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing EVENT_API rule {RuleId} for slug '{Slug}'", rule.Id, slug);
        }
    }

    if (firedCount == 0)
        return Results.NotFound(new { error = $"No active automation rule found for event slug '{slug}'." });

    return Results.Ok(new { status = "success", firedRules = firedCount, slug, message = $"Event '{slug}' triggered {firedCount} automation rule(s)." });
});

app.Lifetime.ApplicationStarted.Register(() => 
{
    try
    {
        var dbFactory = app.Services.GetRequiredService<SqliteConnectionFactory>();
        var schemaPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "HotelAutomation.Data", "Schema.sql");
        var initScript = File.ReadAllText(schemaPath);
        using var conn = dbFactory.OpenAsync().GetAwaiter().GetResult();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = initScript;
        cmd.ExecuteNonQuery();
        
        // Open browser automatically
        var url = app.Urls.FirstOrDefault() ?? "http://localhost:5000";
        var processStartInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        };
        System.Diagnostics.Process.Start(processStartInfo);
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Failed to initialize DB or open browser");
    }
});

app.MapPost("/api/test-webhook", async (string wamid, string status, SqliteConnectionFactory dbFactory) => 
{
    using var conn = await dbFactory.OpenAsync();
    await conn.ExecuteAsync("UPDATE OutboxMessages SET Status = @Status WHERE ExternalMessageId = @Id OR Id = @Id", new { Status = status, Id = wamid });
    return Results.Ok(new { message = $"Status updated to {status}" });
});

app.MapGet("/api/categories", async (CategoryRepository repo) => await repo.GetAllAsync());
app.MapPost("/api/categories", async (HotelAutomation.Core.Models.ContactCategory cat, CategoryRepository repo) => 
{
    var id = await repo.AddAsync(cat);
    return Results.Ok(new { id });
});

app.MapDelete("/api/categories/{id}", async (int id, CategoryRepository repo) => 
{
    var success = await repo.DeleteAsync(id);
    return success ? Results.Ok() : Results.NotFound();
});

app.MapGet("/api/contacts/search", async (string query, ContactRepository repo) => 
{
    if (string.IsNullOrWhiteSpace(query)) return Results.Ok(new List<HotelAutomation.Core.Models.Contact>());
    var results = await repo.SearchContactsAsync(query);
    return Results.Ok(results);
});

app.MapGet("/api/contacts", async (ContactRepository repo, int? categoryId, int page = 1, int limit = 50) => 
{
    var (list, total) = await repo.GetAllAsync(categoryId, page, limit);
    return Results.Ok(new { Contacts = list, TotalCount = total });
});

app.MapGet("/api/contacts/columns/all", async (ContactRepository repo) => 
{
    var columns = await repo.GetDynamicColumnsAsync();
    return Results.Ok(columns);
});

app.MapGet("/api/contacts/columns/dates", async (ContactRepository repo) => 
{
    var columns = await repo.GetDateColumnsAsync();
    return Results.Ok(columns);
});

app.MapDelete("/api/contacts", async (ContactRepository repo) => 
{
    var deletedCount = await repo.DeleteAllAsync();
    return Results.Ok(new { message = $"Deleted {deletedCount} contacts." });
});

app.MapDelete("/api/contacts/{id}", async (int id, ContactRepository repo) => 
{
    var success = await repo.DeleteByIdAsync(id);
    return success ? Results.Ok() : Results.NotFound();
});

app.MapPost("/api/contacts", async (HotelAutomation.Core.Models.Contact contact, ContactRepository repo) => 
{
    var newId = await repo.InsertAsync(contact);
    return Results.Ok(new { id = newId });
});

app.MapPut("/api/contacts/{id}", async (int id, HotelAutomation.Core.Models.Contact contact, ContactRepository repo) => 
{
    var success = await repo.UpdateAsync(id, contact);
    return success ? Results.Ok() : Results.NotFound();
});

app.MapDelete("/api/contacts/columns/{columnName}", async (string columnName, ContactRepository repo) => 
{
    var updatedRows = await repo.DeleteCustomFieldColumnAsync(columnName);
    return Results.Ok(new { message = $"Column '{columnName}' deleted from {updatedRows} contacts." });
});

app.MapPost("/api/contacts/import/chunk", async (List<HotelAutomation.Core.Models.Contact> contacts, ContactRepository repo) => 
{
    await repo.UpsertBulkAsync(contacts);
    return Results.Ok(new { importedCount = contacts.Count });
});

app.MapPost("/api/contacts/import", async (HttpContext ctx, [Microsoft.AspNetCore.Mvc.FromForm] int? categoryId, CsvImportService importService) => 
{
    if (!ctx.Request.HasFormContentType || ctx.Request.Form.Files.Count == 0) return Results.BadRequest("No file uploaded");
    var file = ctx.Request.Form.Files[0];
    using var stream = file.OpenReadStream();
    var count = await importService.ProcessCsvAsync(stream, categoryId);
    return Results.Ok(new { importedCount = count });
}).DisableAntiforgery();

app.MapPost("/api/integrations/pms-webhook", async (
    HttpContext ctx, 
    string pmsName, 
    PmsMappingRepository mappingRepo, 
    ContactRepository contactRepo, 
    EventLogRepository eventLog) => 
{
    var mapping = await mappingRepo.GetByNameAsync(pmsName);
    if (mapping == null) return Results.NotFound($"Mapping for PMS '{pmsName}' not found.");

    using var reader = new StreamReader(ctx.Request.Body);
    var body = await reader.ReadToEndAsync();
    
    try
    {
        var jsonNode = System.Text.Json.Nodes.JsonNode.Parse(body);
        if (jsonNode == null) return Results.BadRequest("Invalid JSON payload.");

        // Minimal dynamic extraction using JSON Path is complex without a full library like JsonPath.Net.
        // For MVP, we assume the mapping defines simple dot-separated paths for key fields:
        // {"PhoneNumber": "guest.phone", "FirstName": "guest.first_name", "LastName": "guest.last_name"}
        var fieldMappings = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(mapping.JsonPathMapping);
        if (fieldMappings == null) return Results.BadRequest("Invalid JsonPathMapping config.");

        string? GetValueByPath(System.Text.Json.Nodes.JsonNode node, string path)
        {
            var parts = path.Split('.');
            var current = node;
            foreach (var part in parts)
            {
                if (current is System.Text.Json.Nodes.JsonObject obj && obj.ContainsKey(part))
                {
                    current = obj[part];
                }
                else return null;
            }
            return current?.ToString();
        }

        var phone = GetValueByPath(jsonNode, fieldMappings.GetValueOrDefault("PhoneNumber", "phone"));
        var firstName = GetValueByPath(jsonNode, fieldMappings.GetValueOrDefault("FirstName", "first_name"));
        var lastName = GetValueByPath(jsonNode, fieldMappings.GetValueOrDefault("LastName", "last_name"));

        if (string.IsNullOrWhiteSpace(phone))
        {
            return Results.BadRequest("Could not extract a valid phone number from the payload.");
        }

        var contact = new HotelAutomation.Core.Models.Contact
        {
            PhoneNumber = phone,
            FirstName = firstName,
            LastName = lastName,
            CategoryId = 1, // Default to a general category or could be mapped
            CustomFieldsJson = body, // Store the entire raw payload for advanced automation
            CreatedAt = DateTimeOffset.UtcNow
        };

        await contactRepo.UpsertAsync(contact);
        
        // Log the event
        await eventLog.LogAsync(new HotelAutomation.Core.Models.PmsEvent 
        { 
            EventType = "PmsWebhook", 
            GuestName = $"{firstName} {lastName}".Trim(), 
            PhoneNumber = phone, 
            RoomNumber = GetValueByPath(jsonNode, fieldMappings.GetValueOrDefault("RoomNumber", "room_number")) ?? "",
            RawPayload = body 
        });

        return Results.Ok(new { message = "Contact updated from PMS payload.", phone });
    }
    catch (Exception ex)
    {
        return Results.BadRequest($"Error parsing payload: {ex.Message}");
    }
});

app.MapGet("/api/integrations/pms-mappings", async (PmsMappingRepository repo) => 
{
    return Results.Ok(await repo.GetAllAsync());
});

app.MapPost("/api/integrations/pms-mappings", async (HotelAutomation.Core.Models.PmsMapping mapping, PmsMappingRepository repo) => 
{
    var newMapping = mapping with { CreatedAt = DateTimeOffset.UtcNow };
    await repo.AddAsync(newMapping);
    return Results.Ok();
});

app.MapDelete("/api/integrations/pms-mappings/{id}", async (int id, PmsMappingRepository repo) => 
{
    await repo.DeleteAsync(id);
    return Results.Ok();
});

app.Run();

public class WebhookPayload { public string From { get; set; } = ""; public string Body { get; set; } = ""; }

/// <summary>Request body for POST /api/broadcast</summary>
public record BroadcastRequest
{
    /// <summary>The ID of the MessageTemplate to send.</summary>
    public int TemplateId { get; init; }

    /// <summary>If set, only send to contacts in this category. Null = all contacts.</summary>
    public int? CategoryId { get; init; }

    /// <summary>Optional JSON with extra template parameters to include in every message.</summary>
    public string? CustomParametersJson { get; init; }
}
