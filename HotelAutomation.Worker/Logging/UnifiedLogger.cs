namespace HotelAutomation.Worker.Logging;

using System.Collections.Concurrent;
using System.Text;
using System.Threading.Channels;
using HotelAutomation.Data.Repositories;
using Microsoft.Extensions.Logging;

public sealed record LogQueueItem(
    string Level,
    string Category,
    string Source,
    string Message,
    string? Details,
    string? Exception
);

public sealed class UnifiedLoggerProvider : ILoggerProvider, IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, UnifiedLogger> _loggers = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _logDirectory;
    private readonly Channel<LogQueueItem> _channel;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _dbWriterTask;
    private SystemLogRepository? _repository;
    private static readonly object _fileLock = new();

    public UnifiedLoggerProvider(string logDirectory)
    {
        _logDirectory = logDirectory;
        Directory.CreateDirectory(_logDirectory);

        // Bounded channel to prevent excessive memory usage while remaining non-blocking
        _channel = Channel.CreateBounded<LogQueueItem>(new BoundedChannelOptions(10000)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });

        _dbWriterTask = Task.Run(ProcessQueueAsync);
    }

    public void SetRepository(SystemLogRepository repository)
    {
        _repository = repository;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return _loggers.GetOrAdd(categoryName, name => new UnifiedLogger(name, this));
    }

    internal void WriteLog(
        LogLevel logLevel,
        string categoryName,
        string message,
        Exception? exception)
    {
        if (string.IsNullOrWhiteSpace(message) && exception == null) return;

        var levelStr = logLevel switch
        {
            LogLevel.Critical => "FATAL",
            LogLevel.Error => "ERROR",
            LogLevel.Warning => "WARN",
            LogLevel.Information => "INFO",
            LogLevel.Debug => "DEBUG",
            LogLevel.Trace => "DEBUG",
            _ => "INFO"
        };

        var category = DetermineCategory(categoryName);
        var source = categoryName;
        var exString = exception?.ToString();

        // 1. Write to Rolling File on Disk
        WriteToFile(levelStr, category, source, message, exString);

        // 2. Queue for asynchronous SQLite insertion
        var item = new LogQueueItem(
            levelStr,
            category,
            source,
            message,
            null,
            exString
        );

        _channel.Writer.TryWrite(item);
    }

    private void WriteToFile(string level, string category, string source, string message, string? exception)
    {
        lock (_fileLock)
        {
            try
            {
                var filePath = Path.Combine(_logDirectory, $"app-{DateTime.Now:yyyy-MM-dd}.log");
                var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level,-5}] [{category}] [{source}] {message}";
                if (!string.IsNullOrEmpty(exception))
                {
                    line += Environment.NewLine + exception;
                }
                File.AppendAllText(filePath, line + Environment.NewLine, Encoding.UTF8);
            }
            catch { /* File write failsafe */ }
        }
    }

    private async Task ProcessQueueAsync()
    {
        var reader = _channel.Reader;
        var batch = new List<LogQueueItem>(50);

        while (!_cts.Token.IsCancellationRequested)
        {
            try
            {
                if (await reader.WaitToReadAsync(_cts.Token))
                {
                    while (batch.Count < 50 && reader.TryRead(out var item))
                    {
                        batch.Add(item);
                    }

                    if (batch.Count > 0 && _repository != null)
                    {
                        foreach (var log in batch)
                        {
                            try
                            {
                                await _repository.LogAsync(
                                    log.Level,
                                    log.Category,
                                    log.Source,
                                    log.Message,
                                    log.Details,
                                    log.Exception,
                                    CancellationToken.None);
                            }
                            catch { /* non-critical */ }
                        }
                        batch.Clear();
                    }
                    else if (_repository == null)
                    {
                        // Repository not wired yet; pause briefly and keep in memory or retry
                        await Task.Delay(200, _cts.Token);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                await Task.Delay(500, _cts.Token).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            }
        }
    }

    private static string DetermineCategory(string categoryName)
    {
        if (categoryName.Contains("WhatsApp", StringComparison.OrdinalIgnoreCase))
            return "WhatsApp";
        if (categoryName.Contains("Pms", StringComparison.OrdinalIgnoreCase) || categoryName.Contains("Pdf", StringComparison.OrdinalIgnoreCase) || categoryName.Contains("HotelWorker", StringComparison.OrdinalIgnoreCase))
            return "PDF & PMS";
        if (categoryName.Contains("TemplateSync", StringComparison.OrdinalIgnoreCase))
            return "Template Sync";
        if (categoryName.Contains("Outbox", StringComparison.OrdinalIgnoreCase))
            return "Outbox";
        if (categoryName.Contains("Campaign", StringComparison.OrdinalIgnoreCase))
            return "Campaigns";
        if (categoryName.Contains("Auth", StringComparison.OrdinalIgnoreCase) || categoryName.Contains("User", StringComparison.OrdinalIgnoreCase) || categoryName.Contains("Security", StringComparison.OrdinalIgnoreCase))
            return "Authentication";
        if (categoryName.Contains("Data", StringComparison.OrdinalIgnoreCase) || categoryName.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) || categoryName.Contains("Database", StringComparison.OrdinalIgnoreCase))
            return "Database";
        if (categoryName.Contains("Crash", StringComparison.OrdinalIgnoreCase) || categoryName.Contains("Exception", StringComparison.OrdinalIgnoreCase))
            return "System Crash";

        return "General";
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        _loggers.Clear();
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        try
        {
            await _dbWriterTask.ConfigureAwait(false);
        }
        catch { }
        _cts.Dispose();
        _loggers.Clear();
    }
}

public sealed class UnifiedLogger : ILogger
{
    private readonly string _categoryName;
    private readonly UnifiedLoggerProvider _provider;

    public UnifiedLogger(string categoryName, UnifiedLoggerProvider provider)
    {
        _categoryName = categoryName;
        _provider = provider;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;

        var message = formatter(state, exception);
        _provider.WriteLog(logLevel, _categoryName, message, exception);
    }
}
