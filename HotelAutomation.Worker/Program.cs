using HotelAutomation.Connectors.Pms;
using HotelAutomation.Connectors.WhatsApp;
using HotelAutomation.Core.Interfaces;
using HotelAutomation.Data;
using HotelAutomation.Data.Repositories;
using HotelAutomation.Worker;

var builder = Host.CreateApplicationBuilder(args);

// ── Database ──────────────────────────────────────────────────────────────────
var connectionString = builder.Configuration.GetConnectionString("Sqlite")
    ?? throw new InvalidOperationException("Missing ConnectionStrings:Sqlite in config.");

builder.Services.AddSingleton(new SqliteConnectionFactory(connectionString));

// ── Repositories ──────────────────────────────────────────────────────────────
builder.Services.AddSingleton<ITemplateRepository, TemplateRepository>();
builder.Services.AddSingleton<IRuleRepository, RuleRepository>();
builder.Services.AddSingleton<IOutboxRepository, OutboxRepository>();
builder.Services.AddSingleton<EventLogRepository>();

// ── WhatsApp Connector ────────────────────────────────────────────────────────
builder.Services.Configure<WhatsAppOptions>(
    builder.Configuration.GetSection(WhatsAppOptions.SectionName));

builder.Services.AddHttpClient<WhatsAppApiClient>();
builder.Services.AddSingleton<IMessageChannel, WhatsAppMessageSender>();

// ── PMS Connector ─────────────────────────────────────────────────────────────
// Register as both the interface AND the concrete type so Worker.cs can
// access RaiseTestEvent() directly for manual testing.
builder.Services.AddSingleton<ManualPmsConnector>();
builder.Services.AddSingleton<IPmsEventSource>(sp => sp.GetRequiredService<ManualPmsConnector>());

// ── Hosted Services ───────────────────────────────────────────────────────────
// Temporarily commented out until Phase 16 and 17 implement these classes.
builder.Services.AddHostedService<HotelWorker>();
builder.Services.AddHostedService<OutboxProcessor>();

var host = builder.Build();
host.Run();
