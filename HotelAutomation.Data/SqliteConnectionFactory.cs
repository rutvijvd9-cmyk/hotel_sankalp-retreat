namespace HotelAutomation.Data;

using Microsoft.Data.Sqlite;
using System.Reflection;

/// <summary>
/// Opens a SQLite connection to the local .db file.
/// On first call, runs Schema.sql to create any missing tables (idempotent).
/// </summary>
public sealed class SqliteConnectionFactory
{
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
    }
}
