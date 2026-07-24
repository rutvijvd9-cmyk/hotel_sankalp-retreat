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

        // Template HeaderType migration
        try
        {
            await using var migCmd = connection.CreateCommand();
            migCmd.CommandText = "ALTER TABLE MessageTemplates ADD COLUMN HeaderType TEXT NOT NULL DEFAULT 'None';";
            await migCmd.ExecuteNonQueryAsync(ct);
        }
        catch { }

        // Template BodyText and ParameterDefinitionsJson migration
        try
        {
            await using var migCmd = connection.CreateCommand();
            migCmd.CommandText = "ALTER TABLE MessageTemplates ADD COLUMN BodyText TEXT NULL;";
            await migCmd.ExecuteNonQueryAsync(ct);
        }
        catch { }

        try
        {
            await using var migCmd = connection.CreateCommand();
            migCmd.CommandText = "ALTER TABLE MessageTemplates ADD COLUMN ParameterDefinitionsJson TEXT NOT NULL DEFAULT '[]';";
            await migCmd.ExecuteNonQueryAsync(ct);
        }
        catch { }

        try
        {
            await using var migCmd = connection.CreateCommand();
            migCmd.CommandText = "ALTER TABLE MessageTemplates ADD COLUMN HeaderText TEXT NULL;";
            await migCmd.ExecuteNonQueryAsync(ct);
        }
        catch { }

        try
        {
            await using var migCmd = connection.CreateCommand();
            migCmd.CommandText = "ALTER TABLE MessageTemplates ADD COLUMN FooterText TEXT NULL;";
            await migCmd.ExecuteNonQueryAsync(ct);
        }
        catch { }

        try
        {
            await using var migCmd = connection.CreateCommand();
            migCmd.CommandText = "ALTER TABLE MessageTemplates ADD COLUMN ButtonsJson TEXT NOT NULL DEFAULT '[]';";
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

        // ConversationReplies table
        try
        {
            await using var migCmd = connection.CreateCommand();
            migCmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS ConversationReplies (
                    Id TEXT PRIMARY KEY,
                    PhoneNumber TEXT NOT NULL,
                    Sender TEXT NOT NULL,
                    Content TEXT NOT NULL,
                    SentAt TEXT NOT NULL
                );";
            await migCmd.ExecuteNonQueryAsync(ct);
        }
        catch { }

        // ShortenedLinks table for Click Tracking
        try
        {
            await using var migCmd = connection.CreateCommand();
            migCmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS ShortenedLinks (
                    ShortCode TEXT PRIMARY KEY,
                    OriginalUrl TEXT NOT NULL,
                    OutboxMessageId INTEGER NOT NULL REFERENCES OutboxMessages(Id),
                    ClickedCount INTEGER NOT NULL DEFAULT 0,
                    FirstClickedAt TEXT NULL,
                    LastClickedAt TEXT NULL
                );";
            await migCmd.ExecuteNonQueryAsync(ct);
        }
        catch { }

        // Campaigns.IsEnabled migration
        try
        {
            await using var migCmd = connection.CreateCommand();
            migCmd.CommandText = "ALTER TABLE Campaigns ADD COLUMN IsEnabled INTEGER NOT NULL DEFAULT 1;";
            await migCmd.ExecuteNonQueryAsync(ct);
        }
        catch { }

        // OutboxMessages.VariantId migration
        try
        {
            await using var migCmd = connection.CreateCommand();
            migCmd.CommandText = "ALTER TABLE OutboxMessages ADD COLUMN VariantId INTEGER REFERENCES TemplateVariants(Id);";
            await migCmd.ExecuteNonQueryAsync(ct);
        }
        catch { }

        // OutboxMessages.ErrorMessage migration
        try
        {
            await using var migCmd = connection.CreateCommand();
            migCmd.CommandText = "ALTER TABLE OutboxMessages ADD COLUMN ErrorMessage TEXT NULL;";
            await migCmd.ExecuteNonQueryAsync(ct);
        }
        catch { }

        // ContactConsent table migration
        try
        {
            await using var migCmd = connection.CreateCommand();
            migCmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS ContactConsent (
                    ContactId INTEGER NOT NULL REFERENCES Contacts(Id),
                    CategoryId INTEGER NOT NULL REFERENCES ContactCategories(Id),
                    IsSubscribed INTEGER NOT NULL DEFAULT 1,
                    UpdatedAt TEXT NOT NULL,
                    PRIMARY KEY (ContactId, CategoryId)
                );";
            await migCmd.ExecuteNonQueryAsync(ct);
        }
        catch { }

        // PmsMappings table migration
        try
        {
            await using var migCmd = connection.CreateCommand();
            migCmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS PmsMappings (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    PmsSystemName TEXT NOT NULL,
                    JsonPathMapping TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL
                );";
            await migCmd.ExecuteNonQueryAsync(ct);
        }
        catch { }

        // TemplateVariants table migration
        try
        {
            await using var migCmd = connection.CreateCommand();
            migCmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS TemplateVariants (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    TemplateId INTEGER NOT NULL REFERENCES MessageTemplates(Id),
                    VariantName TEXT NOT NULL,
                    Body TEXT NOT NULL,
                    Weight INTEGER NOT NULL DEFAULT 50,
                    CreatedAt TEXT NOT NULL
                );";
            await migCmd.ExecuteNonQueryAsync(ct);
        }
        catch { }

        // Contacts UpdatedAt and UpdateCount migrations
        try
        {
            await using var migCmd = connection.CreateCommand();
            migCmd.CommandText = "ALTER TABLE Contacts ADD COLUMN UpdatedAt TEXT NOT NULL DEFAULT (datetime('now'));";
            await migCmd.ExecuteNonQueryAsync(ct);
        }
        catch { }

        try
        {
            await using var migCmd = connection.CreateCommand();
            migCmd.CommandText = "ALTER TABLE Contacts ADD COLUMN UpdateCount INTEGER NOT NULL DEFAULT 0;";
            await migCmd.ExecuteNonQueryAsync(ct);
        }
        catch { }

        try
        {
            await using var migCmd = connection.CreateCommand();
            migCmd.CommandText = "ALTER TABLE Contacts ADD COLUMN CheckInCount INTEGER NOT NULL DEFAULT 0;";
            await migCmd.ExecuteNonQueryAsync(ct);
        }
        catch { }

        try
        {
            await using var migCmd = connection.CreateCommand();
            migCmd.CommandText = "ALTER TABLE Contacts ADD COLUMN AverageStayDays REAL NOT NULL DEFAULT 0;";
            await migCmd.ExecuteNonQueryAsync(ct);
        }
        catch { }

        try
        {
            await using var migCmd = connection.CreateCommand();
            migCmd.CommandText = "ALTER TABLE Contacts ADD COLUMN IsVip INTEGER NOT NULL DEFAULT 0;";
            await migCmd.ExecuteNonQueryAsync(ct);
        }
        catch { }

        // If/Else branching columns on AutomationRules
        try
        {
            await using var migCmd = connection.CreateCommand();
            migCmd.CommandText = "ALTER TABLE AutomationRules ADD COLUMN YesTemplateId INTEGER NULL;";
            await migCmd.ExecuteNonQueryAsync(ct);
        }
        catch { }

        try
        {
            await using var migCmd = connection.CreateCommand();
            migCmd.CommandText = "ALTER TABLE AutomationRules ADD COLUMN NoTemplateId INTEGER NULL;";
            await migCmd.ExecuteNonQueryAsync(ct);
        }
        catch { }
    }
}
