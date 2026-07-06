namespace HotelAutomation.Core.Models
{
    public class PmsEventPayload
    {
        public string GuestName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string RoomNumber { get; set; } = string.Empty;
        public decimal TotalBill { get; set; }
    }
}
