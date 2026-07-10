using System;

namespace HotelAutomation.Core.Models
{
    public class InboxMessage
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string FromPhoneNumber { get; set; } = string.Empty;
        public string SenderName { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public bool IsRead { get; set; } = false;
        public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
