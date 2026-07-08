using System;

namespace HotelAutomation.Core.Models
{
    public class ScheduledJob
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string GuestName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public int TemplateId { get; set; }
        public string ParametersJson { get; set; } = "{}";
        
        // This is the time when the job should be picked up and processed
        public DateTimeOffset TriggerTime { get; set; }
        
        public string Status { get; set; } = "Pending"; // Pending, Processed, Cancelled
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public int? RuleId { get; set; }
    }
}
