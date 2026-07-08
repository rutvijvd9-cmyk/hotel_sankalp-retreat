namespace HotelAutomation.Core.Models;

public sealed record Campaign
{
    public int Id { get; init; }
    public required string Name { get; init; }
    public int TemplateId { get; init; }
    public int TotalContacts { get; init; }
    public string Status { get; init; } = "Processing";
    public string CreatedAt { get; init; } = string.Empty;
}
