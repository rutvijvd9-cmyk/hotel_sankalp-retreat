namespace HotelAutomation.Worker.Services;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

public static class PortAndProcessManager
{
    /// <summary>
    /// Checks for duplicate worker processes or any zombie process listening on the specified port.
    /// Terminates them automatically so that port binding and SQLite file access succeed cleanly.
    /// </summary>
    public static void EnsurePortAndProcessAvailable(int port = 8000)
    {
        // 0. If the server is already active and healthy, open the dashboard in browser and exit gracefully
        if (IsServerAlreadyRunning(port))
        {
            Console.WriteLine($"[Startup] Hotel Automation server is already running on http://localhost:{port}.");
            Console.WriteLine("[Startup] Opening dashboard in default browser...");
            try
            {
                Process.Start(new ProcessStartInfo($"http://localhost:{port}") { UseShellExecute = true });
            }
            catch { }
            Environment.Exit(0);
            return;
        }

        bool killedAny = false;
        int currentPid = Environment.ProcessId;

        // 1. Terminate any other instances of HotelAutomation.Worker
        try
        {
            string currentProcName = Process.GetCurrentProcess().ProcessName;
            var otherInstances = Process.GetProcessesByName(currentProcName)
                .Concat(Process.GetProcessesByName("HotelAutomation.Worker"))
                .Where(p => p.Id != currentPid)
                .GroupBy(p => p.Id)
                .Select(g => g.First())
                .ToList();

            foreach (var proc in otherInstances)
            {
                try
                {
                    Console.WriteLine($"[Startup] Found previous worker instance (PID: {proc.Id}). Terminating...");
                    proc.Kill(entireProcessTree: true);
                    proc.WaitForExit(3000);
                    killedAny = true;
                    Console.WriteLine($"[Startup] Successfully stopped previous worker (PID: {proc.Id}).");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Startup] Warning: Could not stop process {proc.Id}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Startup] Duplicate instance check error: {ex.Message}");
        }

        // 2. Terminate any other process listening on the target port
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                killedAny |= KillProcessesOnPortWindows(port, currentPid);
            }
            else
            {
                killedAny |= KillProcessesOnPortUnix(port, currentPid);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Startup] Port conflict check error: {ex.Message}");
        }

        // 3. If any conflicting processes were killed, pause to allow socket and file handles to be released
        if (killedAny)
        {
            Console.WriteLine($"[Startup] Cleaned up conflicting processes on port {port}. Waiting 1s for port and file locks to release...");
            Thread.Sleep(1000);
        }
    }

    private static bool KillProcessesOnPortWindows(int port, int currentPid)
    {
        bool killed = false;
        var pids = new HashSet<int>();

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c netstat -ano -p tcp | findstr \":{port} \"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process != null)
            {
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit(2000);

                var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    if (line.Contains("LISTENING", StringComparison.OrdinalIgnoreCase))
                    {
                        var parts = line.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length > 0 && int.TryParse(parts[^1], out int pid) && pid > 0)
                        {
                            pids.Add(pid);
                        }
                    }
                }
            }
        }
        catch { }

        foreach (var pid in pids)
        {
            if (pid == currentPid) continue;

            Console.WriteLine($"[Startup] Process PID {pid} is occupying port {port}. Freeing port...");
            try
            {
                using var proc = Process.GetProcessById(pid);
                proc.Kill(true);
                proc.WaitForExit(2000);
                killed = true;
                Console.WriteLine($"[Startup] Killed process {pid} occupying port {port}.");
            }
            catch
            {
                try
                {
                    using var killProc = Process.Start(new ProcessStartInfo
                    {
                        FileName = "taskkill.exe",
                        Arguments = $"/F /T /PID {pid}",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    });
                    killProc?.WaitForExit(2000);
                    killed = true;
                }
                catch { }
            }
        }

        return killed;
    }

    private static bool KillProcessesOnPortUnix(int port, int currentPid)
    {
        bool killed = false;
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "/bin/sh",
                Arguments = $"-c \"lsof -ti :{port}\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process != null)
            {
                string output = process.StandardOutput.ReadToEnd();
                var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    if (int.TryParse(line.Trim(), out int pid) && pid != currentPid && pid > 0)
                    {
                        Console.WriteLine($"[Startup] Process PID {pid} is occupying port {port}. Freeing port...");
                        try
                        {
                            using var proc = Process.GetProcessById(pid);
                            proc.Kill(true);
                            proc.WaitForExit(2000);
                            killed = true;
                        }
                        catch { }
                    }
                }
            }
        }
        catch { }

        return killed;
    }

    private static bool IsServerAlreadyRunning(int port)
    {
        try
        {
            using var handler = new System.Net.Http.SocketsHttpHandler { ConnectTimeout = TimeSpan.FromMilliseconds(600) };
            using var client = new System.Net.Http.HttpClient(handler) { Timeout = TimeSpan.FromMilliseconds(1000) };
            var response = client.GetAsync($"http://127.0.0.1:{port}/ping").GetAwaiter().GetResult();
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
