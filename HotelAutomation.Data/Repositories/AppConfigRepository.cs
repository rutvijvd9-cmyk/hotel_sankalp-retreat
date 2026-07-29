namespace HotelAutomation.Data.Repositories;

using Dapper;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public sealed class AppConfigRepository
{
    private readonly SqliteConnectionFactory _factory;

    public AppConfigRepository(SqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<string?> GetValueAsync(string key, CancellationToken ct = default)
    {
        await using var conn = await _factory.OpenAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<string?>("SELECT Value FROM AppConfig WHERE Key = @key", new { key });
    }

    public async Task SetValueAsync(string key, string value, CancellationToken ct = default)
    {
        await using var conn = await _factory.OpenAsync(ct);
        await conn.ExecuteAsync("""
            INSERT INTO AppConfig (Key, Value)
            VALUES (@key, @value)
            ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value;
            """, new { key, value });
    }

    public async Task<Dictionary<string, string>> GetAllValuesAsync(CancellationToken ct = default)
    {
        await using var conn = await _factory.OpenAsync(ct);
        var rows = await conn.QueryAsync<(string Key, string Value)>("SELECT Key, Value FROM AppConfig");
        var dict = new Dictionary<string, string>();
        foreach (var row in rows)
        {
            dict[row.Key] = row.Value;
        }
        return dict;
    }
}
