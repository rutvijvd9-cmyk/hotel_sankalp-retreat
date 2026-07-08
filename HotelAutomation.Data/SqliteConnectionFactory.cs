namespace HotelAutomation.Data;

using Microsoft.Data.Sqlite;
using System.Reflection;

using Dapper;

public class DateTimeOffsetTypeHandler : SqlMapper.TypeHandler<DateTimeOffset>
{
    public override void SetValue(System.Data.IDbDataParameter parameter, DateTimeOffset value)
    {
        parameter.Value = value.ToString("O");
    }

    public override DateTimeOffset Parse(object value)
    {
        return DateTimeOffset.Parse((string)value);
    }
}

/// <summary>
/// Opens a SQLite connection to the local .db file.
/// On first call, runs Schema.sql to create any missing tables (idempotent).
/// </summary>
public sealed class SqliteConnectionFactory
{
    static SqliteConnectionFactory()
    {
        SqlMapper.AddTypeHandler(new DateTimeOffsetTypeHandler());
    }

    private readonly string _connectionString;
    private bool _schemaEnsured;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public SqliteConnectionFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<SqliteConnection> OpenAsync(CancellationToken ct = default)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);

        if (!_schemaEnsured)
        {
            await _lock.WaitAsync(ct);
            try
            {
                if (!_schemaEnsured)
                {
                    await EnsureSchemaAsync(connection, ct);
                    _schemaEnsured = true;
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        return connection;
    }

    private static async Task EnsureSchemaAsync(SqliteConnection connection, CancellationToken ct)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(
            "HotelAutomation.Data.Schema.sql")
            ?? throw new InvalidOperationException("Schema.sql embedded resource not found.");

        using var reader = new StreamReader(stream);
        var sql = await reader.ReadToEndAsync(ct);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(ct);

        // Safe migration: add BodyParamCount column if it doesn't exist yet (SQLite ignores duplicate columns via try/catch)
        try
        {
            await using var migCmd = connection.CreateCommand();
            migCmd.CommandText = "ALTER TABLE MessageTemplates ADD COLUMN BodyParamCount INTEGER NOT NULL DEFAULT -1;";
            await migCmd.ExecuteNonQueryAsync(ct);
        }
        catch { /* Column already exists — safe to ignore */ }

        // Safe migrations for AutomationRules
        try
        {
            await using var migCmd = connection.CreateCommand();
            migCmd.CommandText = "ALTER TABLE AutomationRules ADD COLUMN Name TEXT NULL;";
            await migCmd.ExecuteNonQueryAsync(ct);
        }
        catch { }
        try
        {
            await using var migCmd = connection.CreateCommand();
            migCmd.CommandText = "ALTER TABLE AutomationRules ADD COLUMN Description TEXT NULL;";
            await migCmd.ExecuteNonQueryAsync(ct);
        }
        catch { }
        try
        {
            await using var migCmd = connection.CreateCommand();
            migCmd.CommandText = "ALTER TABLE AutomationRules ADD COLUMN AudienceFilter TEXT NULL;";
            await migCmd.ExecuteNonQueryAsync(ct);
        }
        catch { }

        // RuleId migrations
        try
        {
            await using var migCmd = connection.CreateCommand();
            migCmd.CommandText = "ALTER TABLE ScheduledJobs ADD COLUMN RuleId INTEGER NULL REFERENCES AutomationRules(Id);";
            await migCmd.ExecuteNonQueryAsync(ct);
        }
        catch { }

        try
        {
            await using var migCmd = connection.CreateCommand();
            migCmd.CommandText = "ALTER TABLE OutboxMessages ADD COLUMN RuleId INTEGER NULL REFERENCES AutomationRules(Id);";
            await migCmd.ExecuteNonQueryAsync(ct);
        }
        catch { }

        try
        {
            await using var migCmd = connection.CreateCommand();
            migCmd.CommandText = "ALTER TABLE OutboxMessages ADD COLUMN FailureReason TEXT NULL;";
            await migCmd.ExecuteNonQueryAsync(ct);
        }
        catch { }

        // Template Category migration
        try
        {
            await using var migCmd = connection.CreateCommand();
            migCmd.CommandText = "ALTER TABLE MessageTemplates ADD COLUMN Category TEXT NOT NULL DEFAULT 'Utility';";
            await migCmd.ExecuteNonQueryAsync(ct);
        }
        catch { }

        // UnsubscribedContacts table
        try
        {
            await using var migCmd = connection.CreateCommand();
            migCmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS UnsubscribedContacts (
                    PhoneNumber TEXT PRIMARY KEY,
                    UnsubscribedAt TEXT NOT NULL,
                    Source TEXT NOT NULL
                );";
            await migCmd.ExecuteNonQueryAsync(ct);
        }
        catch { }
    }
}
