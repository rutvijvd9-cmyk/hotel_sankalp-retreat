namespace HotelAutomation.Core.Models;

public sealed record AutomationRule
{
    public int Id { get; init; }
    public required string TriggerEventType { get; init; }  // e.g. "CheckOut"
    public int TemplateId { get; init; }
    public bool IsEnabled { get; init; } = true;
    public string? ConditionsJson { get; init; }            // reserved for future extra-matching logic
}
