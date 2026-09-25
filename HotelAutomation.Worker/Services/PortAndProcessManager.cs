namespace HotelAutomation.Worker.Services;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;

public static class PortAndProcessManager
{
    /// <summary>
    /// Checks for duplicate worker processes or any zombie process listening on the specified port.
    /// Terminates stale instances so that port binding and SQLite file access succeed cleanly.
    /// If the server is already running and healthy, opens the browser and exits gracefully.
    /// </summary>
    public static void EnsurePortAndProcessAvailable(int port = 8001)
    {
        // If the server is already running and healthy, open the browser and return gracefully
        if (IsServerAlreadyRunning(port))
        {
            Console.WriteLine($"[Startup] Hotel Automation server is already running and healthy on http://localhost:{port}.");
            try
            {
                if (Environment.UserInteractive)
                {
                    Process.Start(new ProcessStartInfo($"http://localhost:{port}") { UseShellExecute = true });
                }
            }
            catch { }
            Environment.Exit(0);
            return;
        }

        int currentPid = Environment.ProcessId;
        Console.WriteLine($"[Startup] Starting Hotel Automation server on port {port}...");

        if (IsPortInUse(port))
        {
            Console.WriteLine($"[Startup] Port {port} is occupied by an unresponsive process. Freeing...");
            KillProcessesOnPort(port, currentPid);
            WaitUntilPortBindable(port, 3);
        }
    }

    // ── Port helpers ──────────────────────────────────────────────────────────────

    /// <summary>Uses .NET IPGlobalProperties - no external process, always reliable.</summary>
    private static bool IsPortInUse(int port)
    {
        try
        {
            return IPGlobalProperties.GetIPGlobalProperties()
                .GetActiveTcpListeners()
                .Any(ep => ep.Port == port);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Actively probes whether port is truly bindable by attempting TcpListener.Start()
    /// in a retry loop. This catches Windows TIME_WAIT / close-wait states that
    /// IPGlobalProperties.GetActiveTcpListeners() misses (it only shows LISTEN state).
    /// Returns when the port is confirmed bindable, or logs a warning after timeout.
    /// </summary>
    private static void WaitUntilPortBindable(int port, int timeoutSeconds = 3)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        int attempt = 0;

        while (DateTime.UtcNow < deadline)
        {
            attempt++;
            TcpListener? probe = null;
            try
            {
                probe = new TcpListener(IPAddress.Loopback, port);
                probe.Start();
                probe.Stop();
                if (attempt > 1)
                    Console.WriteLine($"[Startup] Port {port} confirmed bindable after {attempt} probe(s).");
                return; // Port is free — safe for Kestrel to bind
            }
            catch (SocketException)
            {
                // Port still in TIME_WAIT or held by another process — retry
                if (attempt == 1)
                    Console.WriteLine($"[Startup] Port {port} not yet bindable (Windows TIME_WAIT). Probing...");
                Thread.Sleep(200);
            }
            finally
            {
                try { probe?.Stop(); } catch { }
            }
        }

        // Timeout reached — log a warning but let Kestrel try anyway (may still succeed)
        Console.WriteLine($"[Startup] WARNING: Port {port} bind probe timed out after {timeoutSeconds}s. Kestrel will attempt binding anyway.");
    }

    /// <summary>
    /// Kills every process (except currentPid) currently LISTENING on the given port.
    /// Resolves PIDs by parsing netstat -ano output.
    /// </summary>
    private static bool KillProcessesOnPort(int port, int currentPid)
    {
        if (!IsPortInUse(port))
            return false;

        bool killed = false;

        foreach (var pid in GetPidsOnPort(port))
        {
            if (pid == currentPid) continue;

            Console.WriteLine($"[Startup] PID {pid} is occupying port {port}. Freeing...");
            try
            {
                using var proc = Process.GetProcessById(pid);
                proc.Kill(entireProcessTree: true);
                proc.WaitForExit(800);
                killed = true;
                Console.WriteLine($"[Startup] Killed PID {pid}.");
            }
            catch
            {
                // Fallback via taskkill /F /T
                try
                {
                    using var kp = Process.Start(new ProcessStartInfo
                    {
                        FileName = "taskkill.exe",
                        Arguments = $"/F /T /PID {pid}",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    });
                    kp?.WaitForExit(1000);
                    killed = true;
                    Console.WriteLine($"[Startup] Killed process {pid} occupying port {port}.");
                }
                catch { }
            }
        }

        return killed;
    }

    /// <summary>
    /// Parses netstat -ano to find PIDs that own a LISTENING TCP socket on the given port.
    /// Matches both 0.0.0.0:{port} and 127.0.0.1:{port} correctly.
    /// </summary>
    private static HashSet<int> GetPidsOnPort(int port)
    {
        var pids = new HashSet<int>();
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c netstat -ano -p tcp",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (process == null) return pids;

            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(2000);

            string portToken = $":{port}";
            foreach (var rawLine in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string line = rawLine.Trim();
                if (!line.Contains("LISTENING", StringComparison.OrdinalIgnoreCase)) continue;

                // netstat columns: Proto  LocalAddress  ForeignAddress  State  PID
                var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 5) continue;

                // parts[1] is the local address, e.g. "127.0.0.1:8001" or "0.0.0.0:8001"
                if (!parts[1].EndsWith(portToken, StringComparison.Ordinal)) continue;

                if (int.TryParse(parts[^1], out int pid) && pid > 0)
                    pids.Add(pid);
            }
        }
        catch { }

        return pids;
    }

    // ── Liveness check ────────────────────────────────────────────────────────────

    private static bool IsServerAlreadyRunning(int port)
    {
        try
        {
            using var handler = new System.Net.Http.SocketsHttpHandler
            {
                ConnectTimeout = TimeSpan.FromMilliseconds(300)
            };
            using var client = new System.Net.Http.HttpClient(handler)
            {
                Timeout = TimeSpan.FromMilliseconds(500)
            };
            var response = client.GetAsync($"http://127.0.0.1:{port}/ping").GetAwaiter().GetResult();
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
