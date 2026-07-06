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

// ── WhatsApp Connector ────────────────────────────────────────────────────────
builder.Services.Configure<WhatsAppOptions>(
    builder.Configuration.GetSection(WhatsAppOptions.SectionName));

builder.Services.AddHttpClient<WhatsAppApiClient>();
builder.Services.AddSingleton<IMessageChannel, WhatsAppMessageSender>();

// ── PMS Connector ─────────────────────────────────────────────────────────────
builder.Services.AddSingleton<ManualPmsConnector>();
builder.Services.AddSingleton<IPmsEventSource>(sp => sp.GetRequiredService<ManualPmsConnector>());

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

app.MapPost("/api/test-event", (ManualPmsConnector pms) => 
{
    pms.RaiseTestEvent(new HotelAutomation.Core.Models.PmsEvent
    {
        EventType = "CheckOut",
        GuestName = "Test Guest",
        PhoneNumber = "+1234567890", // dummy test number
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

app.MapPost("/api/webhooks/whatsapp", async (WebhookPayload payload, InboxRepository inboxRepo, JobRepository jobRepo) => 
{
    // 1. Log to Inbox
    var msg = new HotelAutomation.Core.Models.InboxMessage
    {
        FromPhoneNumber = payload.From,
        Content = payload.Body,
        IsRead = false,
        ReceivedAt = DateTimeOffset.UtcNow
    };
    await inboxRepo.AddAsync(msg);

    // 2. CANCEL any pending drip campaign messages for this guest!
    await jobRepo.CancelJobsForPhoneNumberAsync(payload.From);

    return Results.Ok();
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

app.Run();

public class WebhookPayload { public string From { get; set; } = ""; public string Body { get; set; } = ""; }
