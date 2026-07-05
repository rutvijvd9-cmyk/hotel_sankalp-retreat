namespace HotelAutomation.Core.Models;

public sealed record PmsEvent
{
    public required string EventType { get; init; }      // "CheckIn" | "CheckOut" | "BillGenerated"
    public required string GuestName { get; init; }
    public required string PhoneNumber { get; init; }    // E.164 format, e.g. "+919876543210"
    public required string RoomNumber { get; init; }
    public decimal? Amount { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}
