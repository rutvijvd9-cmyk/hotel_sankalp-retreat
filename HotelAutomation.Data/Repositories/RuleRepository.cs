namespace HotelAutomation.Data.Repositories;

using Dapper;
using HotelAutomation.Core.Interfaces;
using HotelAutomation.Core.Models;

public sealed class RuleRepository : IRuleRepository
{
    private readonly SqliteConnectionFactory _factory;

    public RuleRepository(SqliteConnectionFactory factory) => _factory = factory;

    public async Task<IReadOnlyList<AutomationRule>> GetEnabledRulesForEventTypeAsync(
        string eventType, CancellationToken ct = default)
    {
        await using var conn = await _factory.OpenAsync(ct);
        var results = await conn.QueryAsync<AutomationRule>(
            "SELECT * FROM AutomationRules WHERE IsEnabled = 1 AND TriggerEventType = @eventType",
            new { eventType });
        return results.AsList();
    }
}
