namespace HotelAutomation.Worker;

using HotelAutomation.Core.Interfaces;
using HotelAutomation.Core.Models;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

public sealed class SmsMessageSender : IMessageChannel
{
    private readonly ILogger<SmsMessageSender> _logger;

    public string ChannelType => "SMS";

    public SmsMessageSender(ILogger<SmsMessageSender> logger)
    {
        _logger = logger;
    }

    public Task<(bool Success, string? ExternalId, string? FailureReason)> SendAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Sending SMS Fallback to {To}: {Params}", message.ToPhoneNumber, message.ParametersJson);
        
        // Simulate a successful SMS via Twilio/Plivo
        return Task.FromResult((true, (string?)$"sms_{System.Guid.NewGuid():N}", (string?)null));
    }
}
