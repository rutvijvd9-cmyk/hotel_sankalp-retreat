namespace HotelAutomation.Core.Models;

public sealed record PmsEvent
{
    public required string EventType { get; init; }      // "CheckIn" | "CheckOut" | "Feedback" | "Marketing"
    public required string GuestName { get; init; }
    public required string PhoneNumber { get; init; }    // E.164 format, e.g. "+919876543210"
    public required string RoomNumber { get; init; }
    public decimal? Amount { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    // Rich hotel-specific fields for WhatsApp template parameters
    public string? CheckInDate { get; init; }            // e.g. "6 July 2026"
    public string? CheckOutDate { get; init; }           // e.g. "8 July 2026"
    public string? WifiPassword { get; init; }           // Hotel Wi-Fi password for welcome message
    public string? BookingReference { get; init; }       // e.g. "BK-2026-1042"
    public string? GuestFeedbackLink { get; init; }      // Google Review / TripAdvisor link
    public string? HotelName { get; init; }              // e.g. "Sankalp Retreat"
    public string? ExtraNote { get; init; }              // Any extra line to add to the message
    public string? RawPayload { get; init; }             // The raw webhook payload
}
