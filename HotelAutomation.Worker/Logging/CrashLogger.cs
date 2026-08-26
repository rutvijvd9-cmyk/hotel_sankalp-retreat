namespace HotelAutomation.Worker.Logging;

using System.Diagnostics;
using System.Text;

/// <summary>
/// Dedicated crash logger that writes fatal crash and unhandled exception dumps
/// immediately and synchronously to disk (%AppData%\HotelSankalpRetreat\logs\crash-*.log)
/// so that logs are preserved even if the application terminates instantly.
/// </summary>
public static class CrashLogger
{
    private static readonly object _syncRoot = new();
    private static string? _logDirectory;

    public static void Initialize(string logDirectory)
    {
        _logDirectory = logDirectory;
        Directory.CreateDirectory(_logDirectory);
    }

    public static void LogCrash(string crashType, Exception? ex, string? customMessage = null)
    {
        lock (_syncRoot)
        {
            try
            {
                var dir = _logDirectory ?? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "HotelSankalpRetreat",
                    "logs");

                Directory.CreateDirectory(dir);

                var crashFilePath = Path.Combine(dir, $"crash-{DateTime.Now:yyyy-MM-dd}.log");
                var generalFilePath = Path.Combine(dir, $"app-{DateTime.Now:yyyy-MM-dd}.log");

                var sb = new StringBuilder();
                sb.AppendLine("================================================================================");
                sb.AppendLine($"[FATAL CRASH] {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff zzz}");
                sb.AppendLine($"Type: {crashType}");
                if (!string.IsNullOrWhiteSpace(customMessage))
                {
                    sb.AppendLine($"Message: {customMessage}");
                }
                sb.AppendLine($"Machine: {Environment.MachineName} | OS: {Environment.OSVersion} | 64BitOS: {Environment.Is64BitOperatingSystem}");
                sb.AppendLine($"Process ID: {Environment.ProcessId} | WorkingSet: {Process.GetCurrentProcess().WorkingSet64 / (1024 * 1024)} MB");
                sb.AppendLine($"CLR Version: {Environment.Version}");

                if (ex != null)
                {
                    sb.AppendLine($"Exception Type: {ex.GetType().FullName}");
                    sb.AppendLine($"Exception Message: {ex.Message}");
                    sb.AppendLine($"HResult: 0x{ex.HResult:X8}");
                    sb.AppendLine("Stack Trace:");
                    sb.AppendLine(ex.StackTrace ?? "No stack trace available.");

                    var inner = ex.InnerException;
                    int depth = 1;
                    while (inner != null && depth <= 5)
                    {
                        sb.AppendLine($"--- Inner Exception [{depth}]: {inner.GetType().FullName} ---");
                        sb.AppendLine($"Inner Message: {inner.Message}");
                        sb.AppendLine(inner.StackTrace ?? "");
                        inner = inner.InnerException;
                        depth++;
                    }
                }
                sb.AppendLine("================================================================================");
                sb.AppendLine();

                var crashContent = sb.ToString();

                // Synchronous atomic write to ensure crash dump is persisted to disk
                File.AppendAllText(crashFilePath, crashContent, Encoding.UTF8);
                File.AppendAllText(generalFilePath, crashContent, Encoding.UTF8);

                // Also output to standard error console
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine(crashContent);
                Console.ResetColor();
            }
            catch (Exception writeEx)
            {
                try
                {
                    Console.Error.WriteLine($"Failed to write crash log: {writeEx.Message}");
                }
                catch { }
            }
        }
    }
}
