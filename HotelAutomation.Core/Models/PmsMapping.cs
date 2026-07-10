namespace HotelAutomation.Core.Models;

public sealed record PmsMapping
{
    public int Id { get; init; }
    public string PmsSystemName { get; init; } = "";
    public string JsonPathMapping { get; init; } = "";
    public DateTimeOffset CreatedAt { get; init; }
}
