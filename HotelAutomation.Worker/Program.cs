using HotelAutomation.Connectors.Pms;
using HotelAutomation.Connectors.WhatsApp;
using HotelAutomation.Core.Interfaces;
using HotelAutomation.Data;
using HotelAutomation.Data.Repositories;
using HotelAutomation.Worker;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;
using Dapper;

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddSingleton<IRuleRepository, RuleRepository>();
builder.Services.AddSingleton<IOutboxRepository, OutboxRepository>();
builder.Services.AddSingleton<EventLogRepository>();
builder.Services.AddSingleton<JobRepository>();
builder.Services.AddSingleton<InboxRepository>();
builder.Services.AddSingleton<CategoryRepository>();
builder.Services.AddSingleton<ContactRepository>();
builder.Services.AddSingleton<CsvImportService>();
builder.Services.AddSingleton<BroadcastRepository>();

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

// ── Hosted Services (Background Engine) ───────────────────────────────────────
builder.Services.AddHostedService<HotelWorker>();
builder.Services.AddHostedService<OutboxProcessor>();
builder.Services.AddHostedService<JobProcessor>();

var app = builder.Build();
app.UseCors();

app.UseDefaultFiles();
app.UseStaticFiles();

// ── API Endpoints for UI ──────────────────────────────────────────────────────
app.MapGet("/api/templates", async (ITemplateRepository repo) => 
{
    return await repo.GetAllAsync();
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
    var id = await repo.AddAsync(template);
    return Results.Ok(new { id });
});

app.MapPut("/api/templates/{id}", async (int id, HotelAutomation.Core.Models.MessageTemplate template, ITemplateRepository repo) => 
{
    var success = await repo.UpdateAsync(id, template);
    return success ? Results.Ok() : Results.NotFound();
});

app.MapDelete("/api/templates/{id}", async (int id, ITemplateRepository repo) => 
{
    var success = await repo.DeleteAsync(id);
    return success ? Results.Ok() : Results.NotFound();
});

app.MapPost("/api/rules", (HotelAutomation.Core.Models.AutomationRule rule) => 
{
    return Results.Ok(rule);
});

// ── Broadcast Campaign Endpoint ────────────────────────────────────────────
app.MapPost("/api/broadcast", async (BroadcastRequest req, BroadcastRepository broadcastRepo) =>
{
    if (req.TemplateId <= 0) return Results.BadRequest("TemplateId is required.");
    var count = await broadcastRepo.QueueBroadcastAsync(req.TemplateId, req.CategoryId, req.CustomParametersJson ?? "{}");
    return Results.Ok(new { queued = count, message = $"{count} messages queued for broadcast." });
});

app.MapGet("/api/broadcast", async (BroadcastRepository broadcastRepo) =>
    await broadcastRepo.GetRecentBroadcastsAsync(50));

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

app.MapPost("/api/webhooks/whatsapp", async (HttpContext ctx, InboxRepository inboxRepo, JobRepository jobRepo, SqliteConnectionFactory dbFactory) => 
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



app.MapPost("/api/pms/checkin", async (HotelAutomation.Core.Models.PmsEventPayload payload, ManualPmsConnector pms) => 
{
    pms.RaiseTestEvent(new HotelAutomation.Core.Models.PmsEvent
    {
        EventType = "CheckIn",
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
        Timestamp = DateTimeOffset.UtcNow
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
        EventType = "CheckOut",
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
        Timestamp = DateTimeOffset.UtcNow
    });
    return Results.Ok(new { status = "success", message = "CheckOut event queued." });
}).AddEndpointFilter(async (ctx, next) =>
{
    if (!ctx.HttpContext.Request.Headers.TryGetValue("X-API-Key", out var extractedApiKey)) return Results.Unauthorized();
    if (extractedApiKey != "SUPER_SECRET_SCALIFTS_KEY_2026") return Results.Unauthorized();
    return await next(ctx);
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

app.MapGet("/api/contacts", async (int? categoryId, ContactRepository repo) => await repo.GetAllAsync(categoryId));
app.MapPost("/api/contacts", async (HotelAutomation.Core.Models.Contact contact, ContactRepository repo) => 
{
    await repo.UpsertAsync(contact);
    return Results.Ok();
});

app.MapPost("/api/contacts/import", async (HttpContext ctx, [Microsoft.AspNetCore.Mvc.FromForm] int? categoryId, CsvImportService importService) => 
{
    if (!ctx.Request.HasFormContentType || ctx.Request.Form.Files.Count == 0) return Results.BadRequest("No file uploaded");
    var file = ctx.Request.Form.Files[0];
    using var stream = file.OpenReadStream();
    var count = await importService.ProcessCsvAsync(stream, categoryId);
    return Results.Ok(new { importedCount = count });
}).DisableAntiforgery();

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
