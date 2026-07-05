namespace HotelAutomation.Core.Models;

public sealed record OutboxMessage
{
    public int Id { get; init; }
    public required string ToPhoneNumber { get; init; }
    public int TemplateId { get; init; }
    public required string ParametersJson { get; init; }    // JSON array of template variable values
    public string Status { get; init; } = "Pending";        // Pending | Sent | Failed
    public int RetryCount { get; init; } = 0;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? SentAt { get; init; }
}
