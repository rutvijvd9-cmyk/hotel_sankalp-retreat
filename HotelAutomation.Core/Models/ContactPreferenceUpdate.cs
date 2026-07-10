namespace HotelAutomation.Core.Models;

public sealed record ContactPreferenceUpdate
{
    public int CategoryId { get; init; }
    public bool IsSubscribed { get; init; }
}
