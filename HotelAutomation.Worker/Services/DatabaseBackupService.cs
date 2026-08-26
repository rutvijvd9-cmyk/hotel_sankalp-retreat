namespace HotelAutomation.Worker.Services;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HotelAutomation.Data;
using HotelAutomation.Data.Repositories;
using Microsoft.Extensions.Logging;

public record BackupFileInfo(
    string FileName,
    string FilePath,
    long SizeBytes,
    string FormattedSize,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset CreatedAtIst,
    string DownloadUrl
);

public record BackupResult(
    bool Success,
    string Message,
    string? FilePath = null,
    string? FileName = null,
    long SizeBytes = 0
);

public sealed class DatabaseBackupService
{
    private readonly SqliteConnectionFactory _factory;
    private readonly SystemLogRepository _systemLogs;
    private readonly ILogger<DatabaseBackupService> _logger;
    private readonly string _backupFolder;
    private readonly string _dbFilePath;
    private static readonly TimeSpan IstOffset = TimeSpan.FromHours(5.5);
    private static readonly SemaphoreSlim _backupLock = new(1, 1);

    public string BackupFolderPath => _backupFolder;

    public DatabaseBackupService(
        SqliteConnectionFactory factory,
        SystemLogRepository systemLogs,
        ILogger<DatabaseBackupService> logger)
    {
        _factory = factory;
        _systemLogs = systemLogs;
        _logger = logger;

        var appDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "HotelSankalpRetreat");

        _dbFilePath = Path.Combine(appDataDir, "hotel_automation.db");
        _backupFolder = Path.Combine(appDataDir, "backup_date");

        if (!Directory.Exists(_backupFolder))
        {
            Directory.CreateDirectory(_backupFolder);
        }
    }

    /// <summary>
    /// Checks if a backup for the specified IST date already exists in the backup_date folder.
    /// </summary>
    public bool HasBackupForDate(DateTimeOffset istDate)
    {
        if (!Directory.Exists(_backupFolder)) return false;

        var datePrefix = istDate.ToString("yyyy-MM-dd");
        var existing = Directory.GetFiles(_backupFolder, $"*_{datePrefix}*.db")
            .Concat(Directory.GetFiles(_backupFolder, $"*{datePrefix}*.db"))
            .ToArray();

        return existing.Length > 0;
    }

    /// <summary>
    /// Executes a clean, transactional SQLite backup into the backup_date folder.
    /// </summary>
    public async Task<BackupResult> CreateBackupAsync(string triggerSource = "Manual", CancellationToken ct = default)
    {
        await _backupLock.WaitAsync(ct);
        try
        {
            if (!Directory.Exists(_backupFolder))
            {
                Directory.CreateDirectory(_backupFolder);
            }

            var istNow = DateTimeOffset.UtcNow.ToOffset(IstOffset);
            var timestamp = istNow.ToString("yyyy-MM-dd_HH-mm-ss");
            var backupFileName = $"hotel_automation_backup_{timestamp}.db";
            var backupFilePath = Path.Combine(_backupFolder, backupFileName);

            _logger.LogInformation("Initiating database backup via [{TriggerSource}] to {BackupFilePath}", triggerSource, backupFilePath);

            // Use SQLite VACUUM INTO for a clean, consistent snapshot without locking or corrupting WAL mode
            bool vacuumSucceeded = false;
            try
            {
                await using var conn = await _factory.OpenAsync(ct);
                await using var cmd = conn.CreateCommand();
                
                // SQLite parameter for VACUUM INTO
                var normalizedPath = backupFilePath.Replace("\\", "/");
                cmd.CommandText = $"VACUUM INTO '{normalizedPath}';";
                await cmd.ExecuteNonQueryAsync(ct);
                vacuumSucceeded = true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "VACUUM INTO failed, falling back to safe file copy backup.");
            }

            // Fallback to File.Copy if VACUUM INTO had an issue
            if (!vacuumSucceeded || !File.Exists(backupFilePath))
            {
                if (File.Exists(_dbFilePath))
                {
                    File.Copy(_dbFilePath, backupFilePath, overwrite: true);
                }
                else
                {
                    var msg = $"Source database file not found at {_dbFilePath}";
                    _logger.LogError(msg);
                    return new BackupResult(false, msg);
                }
            }

            var fileInfo = new FileInfo(backupFilePath);
            var sizeBytes = fileInfo.Exists ? fileInfo.Length : 0;
            var formattedSize = FormatSize(sizeBytes);

            _logger.LogInformation("Database backup completed successfully: {FileName} ({Size})", backupFileName, formattedSize);

            // Log to SystemLogs repository
            try
            {
                await _systemLogs.LogAsync(
                    "INFO",
                    "DatabaseBackup",
                    triggerSource,
                    $"Database backup created successfully: {backupFileName} ({formattedSize})",
                    $"Path: {backupFilePath} | Size: {sizeBytes} bytes | Trigger: {triggerSource}",
                    null);
            }
            catch { /* non-fatal */ }

            return new BackupResult(true, $"Database backup created successfully: {backupFileName} ({formattedSize})", backupFilePath, backupFileName, sizeBytes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create database backup.");
            try
            {
                await _systemLogs.LogAsync(
                    "ERROR",
                    "DatabaseBackup",
                    triggerSource,
                    $"Failed to create database backup: {ex.Message}",
                    ex.ToString(),
                    ex.ToString());
            }
            catch { /* non-fatal */ }

            return new BackupResult(false, $"Backup failed: {ex.Message}");
        }
        finally
        {
            _backupLock.Release();
        }
    }

    /// <summary>
    /// Lists all backup files in the backup_date folder ordered by creation date descending.
    /// </summary>
    public IReadOnlyList<BackupFileInfo> GetAllBackups()
    {
        if (!Directory.Exists(_backupFolder)) return Array.Empty<BackupFileInfo>();

        var dir = new DirectoryInfo(_backupFolder);
        var files = dir.GetFiles("*.db")
            .OrderByDescending(f => f.CreationTimeUtc)
            .Select(f =>
            {
                var createdUtc = new DateTimeOffset(f.CreationTimeUtc, TimeSpan.Zero);
                var createdIst = createdUtc.ToOffset(IstOffset);
                return new BackupFileInfo(
                    f.Name,
                    f.FullName,
                    f.Length,
                    FormatSize(f.Length),
                    createdUtc,
                    createdIst,
                    $"/api/database/backups/download/{Uri.EscapeDataString(f.Name)}"
                );
            })
            .ToList();

        return files;
    }

    /// <summary>
    /// Deletes a specific backup file from the backup_date folder.
    /// </summary>
    public bool DeleteBackup(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return false;

        // Prevent directory traversal
        var safeFileName = Path.GetFileName(fileName);
        var filePath = Path.Combine(_backupFolder, safeFileName);

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            _logger.LogInformation("Deleted backup file: {FileName}", safeFileName);
            return true;
        }

        return false;
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F2} MB";
    }
}
