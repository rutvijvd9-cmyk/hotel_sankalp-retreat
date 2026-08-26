namespace HotelAutomation.Core.Models;

public sealed record SystemLogEntry
{
    public int Id { get; init; }
    public string Timestamp { get; init; } = DateTimeOffset.UtcNow.ToString("O");
    public string LogLevel { get; init; } = "INFO"; // FATAL, ERROR, WARN, INFO, DEBUG
    public string Category { get; init; } = "General"; // SystemCrash, WhatsApp, PMS, PDFExtractor, TemplateSync, Auth, Database, Campaign, Outbox, General
    public string Source { get; init; } = "";
    public string Message { get; init; } = "";
    public string? Details { get; init; }
    public string? Exception { get; init; }
}
