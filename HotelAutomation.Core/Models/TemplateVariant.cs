namespace HotelAutomation.Core.Models;

public sealed record TemplateVariant
{
    public int Id { get; init; }
    public int TemplateId { get; init; }
    public string VariantName { get; init; } = "";
    public string Body { get; init; } = "";
    public int Weight { get; init; } = 50; // Default weight
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
