namespace HotelAutomation.Core.Interfaces;

using HotelAutomation.Core.Models;

public interface IRuleRepository
{
    Task<IReadOnlyList<AutomationRule>> GetEnabledRulesForEventTypeAsync(
        string eventType, CancellationToken cancellationToken = default);
}
