namespace HotelAutomation.Core.Models;

/// <summary>
/// The JSON body accepted by /api/pms/checkin and /api/pms/checkout.
/// This is the contract for any external hotel software calling our webhook.
/// </summary>
public sealed record PmsEventPayload
{
    // Required fields
    public required string GuestName { get; init; }
    public required string PhoneNumber { get; init; }   // E.164 format: +919876543210
    public string? RoomNumber { get; init; }

    // Optional billing
    public decimal? TotalBill { get; init; }

    // Rich guest journey fields
    public string? CheckInDate { get; init; }           // "6 July 2026"
    public string? CheckOutDate { get; init; }          // "8 July 2026"
    public string? WifiPassword { get; init; }          // Hotel Wi-Fi password
    public string? BookingReference { get; init; }      // Booking/reservation number
    public string? GuestFeedbackLink { get; init; }     // Google Review or TripAdvisor link
    public string? HotelName { get; init; }             // Defaults to config if not supplied
    public string? ExtraNote { get; init; }             // Any additional info line

    // Capture any other dynamic fields the user sends in their API requests
    [System.Text.Json.Serialization.JsonExtensionData]
    public Dictionary<string, System.Text.Json.JsonElement>? ExtensionData { get; set; }
}
