namespace HotelAutomation.Connectors.Pms;

using HotelAutomation.Core.Interfaces;
using HotelAutomation.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Data.Sqlite;
using Dapper;

/// <summary>
/// Polls the existing hotel management software's database every 30 seconds.
/// Works with any SQL-based hotel software (eZee, Hotelogix, IDS Next, custom systems).
/// Only ever READS from the external DB — never writes, never modifies.
/// 
/// HOW IT WORKS:
/// 1. Every 30 seconds, it reads newly modified CheckIn / CheckOut records from the PMS DB.
/// 2. We track the last-processed record timestamp in our own local SQLite.
/// 3. Any new record fires a PmsEvent into the automation pipeline.
/// 
/// TO CONNECT TO YOUR CLIENT'S PMS:
/// In appsettings.json, set:
///   "PmsDatabase": {
///     "ConnectionString": "Data Source=C:\\HotelSoftware\\hotel.db",   // for SQLite
///     // OR for SQL Server: "Server=localhost;Database=HotelPMS;User Id=readonly_user;Password=xxx;"
///     "DatabaseType": "SQLite",   // "SQLite" | "SqlServer" | "MySQL" | "PostgreSQL"
///     "CheckInTableName": "CheckIns",
///     "CheckOutTableName": "CheckOuts",
///     "GuestNameColumn": "GuestName",
///     "PhoneColumn": "PhoneNumber",
///     "RoomColumn": "RoomNumber",
///     "AmountColumn": "TotalAmount",
///     "TimestampColumn": "CreatedAt"
///   }
/// </summary>
public sealed class DatabasePmsConnector : IPmsEventSource
{
    private readonly DatabasePmsOptions _options;
    private readonly ILogger<DatabasePmsConnector> _logger;
    private readonly string _localDbPath;
    private CancellationTokenSource? _cts;
    private Task? _pollingTask;

    public event EventHandler<PmsEvent>? EventReceived;

    public DatabasePmsConnector(
        DatabasePmsOptions options,
        ILogger<DatabasePmsConnector> logger,
        string localDbPath)
    {
        _options = options;
        _logger = logger;
        _localDbPath = localDbPath;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("DatabasePmsConnector starting. Polling PMS every {Interval}s...",
            _options.PollingIntervalSeconds);
        EnsureTrackingTableExists();
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _pollingTask = PollLoopAsync(_cts.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("DatabasePmsConnector stopping.");
        if (_cts is not null)
        {
            await _cts.CancelAsync();
            if (_pollingTask is not null)
                await _pollingTask.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }
    }

    private async Task PollLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await PollForNewEventsAsync(ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _logger.LogError(ex, "Error polling PMS database. Will retry in {Interval}s.",
                    _options.PollingIntervalSeconds);
            }

            await Task.Delay(TimeSpan.FromSeconds(_options.PollingIntervalSeconds), ct)
                .ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }
    }

    private async Task PollForNewEventsAsync(CancellationToken ct)
    {
        var lastProcessedCheckIn = GetLastProcessed("CheckIn");
        var lastProcessedCheckOut = GetLastProcessed("CheckOut");

        // Connect to the external hotel software's database
        // This example shows SQLite. For SQL Server, swap Microsoft.Data.Sqlite for Microsoft.Data.SqlClient.
        await using var externalConn = new Microsoft.Data.Sqlite.SqliteConnection(_options.ConnectionString);
        await externalConn.OpenAsync(ct);

        // ── Poll for new Check-Ins ──────────────────────────────────────
        var checkInSql = $"""
            SELECT 
                {_options.GuestNameColumn}   AS GuestName,
                {_options.PhoneColumn}       AS PhoneNumber,
                {_options.RoomColumn}        AS RoomNumber,
                {_options.AmountColumn}      AS Amount,
                {_options.TimestampColumn}   AS EventTime
            FROM {_options.CheckInTableName}
            WHERE {_options.TimestampColumn} > @LastProcessed
            ORDER BY {_options.TimestampColumn} ASC
            LIMIT 100
            """;

        var checkIns = await externalConn.QueryAsync(checkInSql, new { LastProcessed = lastProcessedCheckIn });
        foreach (var row in checkIns)
        {
            var evt = new PmsEvent
            {
                EventType = "CheckIn",
                GuestName = (string)(row.GuestName ?? "Guest"),
                PhoneNumber = NormalizePhone((string)(row.PhoneNumber ?? "")),
                RoomNumber = (string)(row.RoomNumber ?? ""),
                Amount = row.Amount is not null ? (decimal?)Convert.ToDecimal(row.Amount) : null,
                Timestamp = DateTimeOffset.UtcNow
            };

            if (string.IsNullOrWhiteSpace(evt.PhoneNumber))
            {
                _logger.LogWarning("Skipping CheckIn for {Guest} — no phone number in PMS record.", evt.GuestName);
                continue;
            }

            _logger.LogInformation("New CheckIn from PMS DB: {Guest} in room {Room}", evt.GuestName, evt.RoomNumber);
            EventReceived?.Invoke(this, evt);
            SetLastProcessed("CheckIn", (string)(row.EventTime?.ToString() ?? DateTimeOffset.UtcNow.ToString("O")));
        }

        // ── Poll for new Check-Outs ────────────────────────────────────
        var checkOutSql = $"""
            SELECT 
                {_options.GuestNameColumn}   AS GuestName,
                {_options.PhoneColumn}       AS PhoneNumber,
                {_options.RoomColumn}        AS RoomNumber,
                {_options.AmountColumn}      AS Amount,
                {_options.TimestampColumn}   AS EventTime
            FROM {_options.CheckOutTableName}
            WHERE {_options.TimestampColumn} > @LastProcessed
            ORDER BY {_options.TimestampColumn} ASC
            LIMIT 100
            """;

        var checkOuts = await externalConn.QueryAsync(checkOutSql, new { LastProcessed = lastProcessedCheckOut });
        foreach (var row in checkOuts)
        {
            var evt = new PmsEvent
            {
                EventType = "CheckOut",
                GuestName = (string)(row.GuestName ?? "Guest"),
                PhoneNumber = NormalizePhone((string)(row.PhoneNumber ?? "")),
                RoomNumber = (string)(row.RoomNumber ?? ""),
                Amount = row.Amount is not null ? (decimal?)Convert.ToDecimal(row.Amount) : null,
                Timestamp = DateTimeOffset.UtcNow
            };

            if (string.IsNullOrWhiteSpace(evt.PhoneNumber))
            {
                _logger.LogWarning("Skipping CheckOut for {Guest} — no phone number in PMS record.", evt.GuestName);
                continue;
            }

            _logger.LogInformation("New CheckOut from PMS DB: {Guest} in room {Room}, Amount: {Amount}", 
                evt.GuestName, evt.RoomNumber, evt.Amount);
            EventReceived?.Invoke(this, evt);
            SetLastProcessed("CheckOut", (string)(row.EventTime?.ToString() ?? DateTimeOffset.UtcNow.ToString("O")));
        }
    }

    // ── Local tracking (so we don't resend the same message) ─────────────────

    private void EnsureTrackingTableExists()
    {
        using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={_localDbPath}");
        conn.Open();
        conn.Execute("""
            CREATE TABLE IF NOT EXISTS PmsPollingState (
                EventType TEXT PRIMARY KEY,
                LastProcessedTimestamp TEXT NOT NULL
            )
            """);
    }

    private string GetLastProcessed(string eventType)
    {
        using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={_localDbPath}");
        conn.Open();
        return conn.ExecuteScalar<string>(
            "SELECT LastProcessedTimestamp FROM PmsPollingState WHERE EventType = @EventType",
            new { EventType = eventType }) ?? "1970-01-01T00:00:00Z";
    }

    private void SetLastProcessed(string eventType, string timestamp)
    {
        using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={_localDbPath}");
        conn.Open();
        conn.Execute("""
            INSERT INTO PmsPollingState (EventType, LastProcessedTimestamp) VALUES (@EventType, @Timestamp)
            ON CONFLICT(EventType) DO UPDATE SET LastProcessedTimestamp = excluded.LastProcessedTimestamp
            """, new { EventType = eventType, Timestamp = timestamp });
    }

    // ── Phone normalisation ────────────────────────────────────────────────────

    /// <summary>
    /// Tries to normalize a phone number to E.164 format (+919876543210).
    /// Indian numbers without country code get +91 prepended.
    /// </summary>
    private static string NormalizePhone(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        raw = new string(raw.Where(c => char.IsDigit(c) || c == '+').ToArray());
        if (raw.StartsWith("+")) return raw;
        // Indian 10-digit mobile numbers
        if (raw.Length == 10 && (raw.StartsWith("6") || raw.StartsWith("7") || raw.StartsWith("8") || raw.StartsWith("9")))
            return "+91" + raw;
        // Indian numbers with 0 prefix
        if (raw.Length == 11 && raw.StartsWith("0"))
            return "+91" + raw[1..];
        // Already has country code digits
        if (raw.Length >= 12)
            return "+" + raw;
        return raw;
    }
}

/// <summary>
/// Configuration for DatabasePmsConnector.
/// Bind from appsettings.json section "PmsDatabase".
/// </summary>
public sealed class DatabasePmsOptions
{
    public const string SectionName = "PmsDatabase";

    /// <summary>Connection string to the hotel software's database.</summary>
    public string ConnectionString { get; set; } = "";

    /// <summary>How often to check for new records (seconds). Default 30.</summary>
    public int PollingIntervalSeconds { get; set; } = 30;

    // Table and column names (customize to match the hotel software's schema)
    public string CheckInTableName { get; set; } = "CheckIns";
    public string CheckOutTableName { get; set; } = "CheckOuts";
    public string GuestNameColumn { get; set; } = "GuestName";
    public string PhoneColumn { get; set; } = "PhoneNumber";
    public string RoomColumn { get; set; } = "RoomNumber";
    public string AmountColumn { get; set; } = "TotalAmount";
    public string TimestampColumn { get; set; } = "CreatedAt";
}
