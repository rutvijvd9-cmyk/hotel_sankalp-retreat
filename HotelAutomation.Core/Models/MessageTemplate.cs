namespace HotelAutomation.Core.Models;

public sealed record MessageTemplate
{
    public int Id { get; init; }
    public required string Name { get; init; }                    // internal label, e.g. "Checkout Bill"
    public required string ExternalTemplateName { get; init; }   // exact Meta-approved name
    public string LanguageCode { get; init; } = "en";
    public string ChannelType { get; init; } = "WhatsApp";
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
