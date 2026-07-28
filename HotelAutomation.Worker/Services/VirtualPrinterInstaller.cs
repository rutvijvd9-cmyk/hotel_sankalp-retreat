namespace HotelAutomation.Worker.Services;

using System.Diagnostics;
using System.Runtime.InteropServices;
using HotelAutomation.Connectors.Pms;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Installs "Hotel Sankalp Printer" as a real Windows virtual printer on startup.
///
/// HOW IT WORKS:
///   1. Only runs on Windows (safely skipped on macOS/Linux dev machines).
///   2. Checks if the printer is already installed — no duplicate installs.
///   3. Ensures the "Microsoft Print To PDF" driver is available (built into Windows 10/11).
///   4. Creates a LOCAL PORT named with the exact output file path.
///      A Windows local port with a file path always writes to that file — no save dialog!
///   5. Adds the printer with the PDF driver and the file port.
///
/// WHAT HOTEL STAFF SEE:
///   When they click "Print" in the old PMS software, they see "Hotel Sankalp Printer"
///   in the printer list. They select it, click Print — the bill PDF is automatically
///   saved to C:\HotelPrints\Output\sankalp_bill.pdf — and within seconds a WhatsApp
///   message is sent to the guest. No other action required.
///
/// DRIVER:
///   Uses "Microsoft Print To PDF" — a built-in Windows 10/11 driver.
///   No 3rd party software (Ghostscript, CutePDF, etc.) required.
///
/// PERMISSIONS:
///   Adding a LOCAL printer (non-shared, no driver install needed since the driver
///   is already present) generally does NOT require administrator rights on Windows 10/11.
///   If it fails with access denied, the installer logs a clear message with manual steps.
/// </summary>
public sealed class VirtualPrinterInstaller : IHostedService
{
    private const string PdfDriverName = "Microsoft Print To PDF";

    private readonly PdfBridgeOptions _options;
    private readonly ILogger<VirtualPrinterInstaller> _logger;

    public VirtualPrinterInstaller(
        PdfBridgeOptions options,
        ILogger<VirtualPrinterInstaller> logger)
    {
        _options = options;
        _logger  = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Only install on Windows — silently skip on macOS/Linux dev machines
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            _logger.LogInformation(
                "VirtualPrinterInstaller: skipping — not running on Windows. " +
                "Printer installation only runs on the hotel's Windows 10/11 computer.");
            return;
        }

        var printerName = _options.VirtualPrinterName;

        // Resolve the {AppDir} placeholder so the printer port points to the real installed path
        var appDir     = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var outputFile = _options.PrinterOutputFile.Replace("{AppDir}", appDir, StringComparison.OrdinalIgnoreCase);

        if (IsPrinterAlreadyInstalled(printerName))
        {
            _logger.LogInformation(
                "Virtual Printer: '{Printer}' is already installed. No action needed.",
                printerName);
            return;
        }

        _logger.LogInformation(
            "Virtual Printer: '{Printer}' not found. Installing now...", printerName);

        try
        {
            await InstallPrinterAsync(printerName, outputFile, cancellationToken);

            if (IsPrinterAlreadyInstalled(printerName))
            {
                _logger.LogInformation(
                    "✅ Virtual Printer: '{Printer}' installed successfully! " +
                    "Hotel staff can now select it when printing bills. " +
                    "PDFs will be saved to: {OutputFile}",
                    printerName, outputFile);
            }
            else
            {
                LogManualInstallInstructions(printerName, outputFile);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Virtual Printer: installation failed for '{Printer}'. " +
                "See manual instructions below.", printerName);
            LogManualInstallInstructions(printerName, outputFile);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    // ── Installation logic ────────────────────────────────────────────────────

    private async Task InstallPrinterAsync(
        string printerName, string outputFile, CancellationToken ct)
    {
        // Ensure output directory exists
        var outputDir = Path.GetDirectoryName(outputFile) ?? @"C:\HotelPrints\Output";
        if (!Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
            _logger.LogInformation("Virtual Printer: created output directory '{Dir}'", outputDir);
        }

        // ── Build PowerShell script ────────────────────────────────────────────
        // We use a LOCAL PORT whose name IS the file path.
        // Windows local ports with file paths write directly to that file — no dialog box.
        var portName   = EscapePs(outputFile);    // e.g. C:\HotelPrints\Output\sankalp_bill.pdf
        var escapedDir = EscapePs(outputDir);
        var escapedName = EscapePs(printerName);

        // Build the PowerShell script as a plain string — avoids raw-string interpolation issues
        var script = string.Join(Environment.NewLine,
            "$ErrorActionPreference = 'Stop'",
            "",
            "# Step 1: Create output directory and placeholder file",
            $"New-Item -ItemType Directory -Force -Path '{escapedDir}' | Out-Null",
            $"if (-not (Test-Path '{portName}')) {{ New-Item -ItemType File -Force -Path '{portName}' | Out-Null }}",
            "",
            "# Step 2: Add local port (port name = file path — no save dialog!)",
            $"$portExists = Get-PrinterPort -Name '{portName}' -ErrorAction SilentlyContinue",
            "if (-not $portExists) {",
            $"    Add-PrinterPort -Name '{portName}'",
            $"    Write-Host 'Port created: {portName}'",
            "} else {",
            "    Write-Host 'Port already exists.'",
            "}",
            "",
            "# Step 3: Install the printer using the built-in PDF driver",
            "Add-Printer `",
            $"    -Name '{escapedName}' `",
            $"    -DriverName '{PdfDriverName}' `",
            $"    -PortName '{portName}' `",
            "    -Comment 'Scalifts — auto-installed by Scalifts app'",
            "",
            $"Write-Host 'Printer installed: {escapedName}'"
        );

        _logger.LogDebug("Virtual Printer: running PowerShell installer script...");
        var (exitCode, stdout, stderr) = await RunPowerShellAsync(script, ct);

        if (!string.IsNullOrWhiteSpace(stdout))
            _logger.LogDebug("Virtual Printer (PS stdout): {Out}", stdout);
        if (!string.IsNullOrWhiteSpace(stderr))
            _logger.LogWarning("Virtual Printer (PS stderr): {Err}", stderr);

        if (exitCode != 0)
        {
            throw new InvalidOperationException(
                $"PowerShell script exited with code {exitCode}. stderr: {stderr}");
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Uses PowerShell Get-Printer to check if the named printer exists on the system.
    /// Avoids the System.Drawing.Common NuGet dependency.
    /// </summary>
    private static bool IsPrinterAlreadyInstalled(string printerName)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName               = "powershell.exe",
                Arguments              = $"-NoProfile -NonInteractive -Command " +
                                         $"\"(Get-Printer -Name '{EscapePs(printerName)}' -ErrorAction SilentlyContinue) -ne $null\"",
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true
            };
            using var process = Process.Start(psi);
            if (process is null) return false;
            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();
            return string.Equals(output, "True", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunPowerShellAsync(
        string script, CancellationToken ct)
    {
        // Write script to a temp file to avoid command-line length limits
        var tempScript = Path.Combine(Path.GetTempPath(), $"hotel_printer_{Guid.NewGuid():N}.ps1");
        await File.WriteAllTextAsync(tempScript, script, ct);

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName               = "powershell.exe",
                Arguments              = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"{tempScript}\"",
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true
            };

            using var process = Process.Start(psi)
                ?? throw new InvalidOperationException("Could not start powershell.exe");

            var stdout = await process.StandardOutput.ReadToEndAsync(ct);
            var stderr = await process.StandardError.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);

            return (process.ExitCode, stdout.Trim(), stderr.Trim());
        }
        finally
        {
            try { File.Delete(tempScript); } catch { /* best-effort cleanup */ }
        }
    }

    /// <summary>Escapes single quotes in a string for safe PowerShell embedding.</summary>
    private static string EscapePs(string value) => value.Replace("'", "''");

    /// <summary>
    /// Logs step-by-step manual instructions so hotel staff can install the printer
    /// themselves if automatic installation fails (e.g. due to permissions).
    /// </summary>
    private void LogManualInstallInstructions(string printerName, string outputFile)
    {
        _logger.LogWarning("""
            ══════════════════════════════════════════════════════════════
            MANUAL PRINTER INSTALLATION REQUIRED
            ══════════════════════════════════════════════════════════════
            Automatic installation of '{Printer}' failed.
            Please follow these steps manually (takes ~2 minutes):

            1. Press Win + R → type: control printers → press Enter
            2. Click "Add a printer"
            3. Click "The printer I want isn't listed"
            4. Select "Add a local printer or network printer with manual settings"
            5. Choose "Create a new port" → Type: Local Port → click Next
            6. Enter the port name exactly: {OutputFile}
            7. Click OK
            8. Select driver: Microsoft → Microsoft Print To PDF → click Next
            9. Printer name: {Printer}
            10. Click Finish

            After these steps, restart the Hotel Automation app.
            ══════════════════════════════════════════════════════════════
            """,
            printerName, outputFile, printerName);
    }
}
