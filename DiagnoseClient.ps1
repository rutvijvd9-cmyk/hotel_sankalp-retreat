# ============================================================
#  DiagnoseClient.ps1
#  Run this on the CLIENT'S PC to find out why the app
#  is not launching.
#  Right-click → Run with PowerShell
# ============================================================

$AppName   = "HotelAutomation.Worker"
$InstallDir = Join-Path $env:ProgramFiles "TheSankalpRetreat"
$ExePath    = Join-Path $InstallDir "$AppName.exe"
$LogDir     = Join-Path $env:APPDATA "HotelSankalpRetreat\logs"
$ReportPath = Join-Path $env:USERPROFILE "Desktop\SankalpDiagReport.txt"

$report = [System.Text.StringBuilder]::new()
function Log($msg) {
    Write-Host $msg
    $report.AppendLine($msg) | Out-Null
}

Log "============================================================"
Log "  Hotel Sankalp Retreat — Diagnostic Report"
Log "  Generated: $(Get-Date)"
Log "============================================================"
Log ""

# 1. Windows Version
Log "── 1. Windows Version ──────────────────────────────────────"
$os = Get-CimInstance Win32_OperatingSystem
Log "  Caption   : $($os.Caption)"
Log "  Build     : $($os.BuildNumber)"
Log "  Version   : $($os.Version)"
$build = [int]$os.BuildNumber
if ($build -lt 19041) {
    Log "  ⚠️  WARNING: Build $build is BELOW the required 19041 (Windows 10 v2004)."
    Log "  The app REQUIRES Windows 10 version 2004 or newer."
} else {
    Log "  ✅ Build OK (>= 19041)"
}
Log ""

# 2. Architecture
Log "── 2. Architecture ─────────────────────────────────────────"
Log "  Is 64-bit OS : $([Environment]::Is64BitOperatingSystem)"
if (-not [Environment]::Is64BitOperatingSystem) {
    Log "  ❌ ERROR: App requires 64-bit Windows. This machine is 32-bit."
} else {
    Log "  ✅ 64-bit OK"
}
Log ""

# 3. Install folder
Log "── 3. Installation Directory ────────────────────────────────"
Log "  Expected : $InstallDir"
if (Test-Path $InstallDir) {
    Log "  ✅ Folder exists"
    $files = Get-ChildItem $InstallDir -ErrorAction SilentlyContinue | Measure-Object
    Log "  Files    : $($files.Count) items"
} else {
    Log "  ❌ ERROR: Install folder NOT FOUND. Installer may have failed."
}
Log ""

# 4. Exe
Log "── 4. Executable ───────────────────────────────────────────"
if (Test-Path $ExePath) {
    $exe = Get-Item $ExePath
    Log "  ✅ Found : $ExePath"
    Log "  Size    : $([math]::Round($exe.Length/1MB,1)) MB"
    Log "  Modified: $($exe.LastWriteTime)"
} else {
    Log "  ❌ ERROR: $ExePath NOT FOUND"
}
Log ""

# 5. Is app currently running?
Log "── 5. Process Status ───────────────────────────────────────"
$procs = Get-Process -Name $AppName -ErrorAction SilentlyContinue
if ($procs) {
    Log "  ✅ App IS running (PID: $($procs[0].Id))"
} else {
    Log "  ❌ App is NOT running"
}
Log ""

# 6. Port 8001
Log "── 6. Port 8001 Status ─────────────────────────────────────"
$port = Get-NetTCPConnection -LocalPort 8001 -State Listen -ErrorAction SilentlyContinue
if ($port) {
    Log "  ✅ Something is LISTENING on port 8001 (PID: $($port[0].OwningProcess))"
} else {
    Log "  ❌ Nothing on port 8001 — app is not running"
}
Log ""

# 7. Crash Logs
Log "── 7. Crash Logs ───────────────────────────────────────────"
Log "  Log folder: $LogDir"
if (Test-Path $LogDir) {
    $logs = Get-ChildItem $LogDir -Filter "crash-*.log" | Sort-Object LastWriteTime -Descending
    if ($logs.Count -gt 0) {
        Log "  ⚠️  Found $($logs.Count) crash log(s):"
        foreach ($l in $logs | Select-Object -First 3) {
            Log ""
            Log "  ── $($l.Name) ──"
            Get-Content $l.FullName -ErrorAction SilentlyContinue | Select-Object -Last 40 | ForEach-Object { Log "  $_" }
        }
    } else {
        Log "  No crash-*.log files found"
        $appLogs = Get-ChildItem $LogDir -Filter "app-*.log" | Sort-Object LastWriteTime -Descending
        if ($appLogs.Count -gt 0) {
            Log "  Found app log(s): $($appLogs[0].Name)"
            Get-Content $appLogs[0].FullName -ErrorAction SilentlyContinue | Select-Object -Last 20 | ForEach-Object { Log "  $_" }
        } else {
            Log "  ❌ No log files at all — app crashed before it could write any logs"
        }
    }
} else {
    Log "  ❌ Log folder does NOT exist — app never ran or crashed immediately"
}
Log ""

# 8. Try launching manually and capture output
Log "── 8. Manual Launch Test ───────────────────────────────────"
if (Test-Path $ExePath) {
    Log "  Attempting to launch app and capture any immediate error..."
    try {
        $psi = New-Object System.Diagnostics.ProcessStartInfo
        $psi.FileName = $ExePath
        $psi.WorkingDirectory = $InstallDir
        $psi.UseShellExecute = $false
        $psi.RedirectStandardOutput = $true
        $psi.RedirectStandardError  = $true
        $psi.CreateNoWindow = $true
        $p = [System.Diagnostics.Process]::Start($psi)
        $stdout = $p.StandardOutput.ReadToEnd()
        $stderr = $p.StandardError.ReadToEnd()
        $p.WaitForExit(5000)
        Log "  Exit code : $($p.ExitCode)"
        if ($stdout) { Log "  STDOUT:`n$stdout" }
        if ($stderr) { Log "  STDERR:`n$stderr" }
        if (-not $stdout -and -not $stderr) { Log "  (No console output — WinExe mode, check crash logs above)" }
    } catch {
        Log "  ❌ Launch failed: $_"
    }
} else {
    Log "  ❌ Skipped — exe not found"
}
Log ""

# 9. Scheduled Tasks (Watchdog & AutoStart)
Log "── 9. Scheduled Tasks ──────────────────────────────────────"
$tasks = @("TheSankalpRetreat\TheSankalpRetreatAutoStart", "TheSankalpRetreat\TheSankalpRetreatWatchdog")
foreach ($t in $tasks) {
    $task = Get-ScheduledTask -TaskPath "\TheSankalpRetreat\" -TaskName ($t -split "\\")[1] -ErrorAction SilentlyContinue
    if ($task) {
        Log "  ✅ $t — State: $($task.State)"
    } else {
        Log "  ❌ $t — NOT FOUND (installer post-config may have failed)"
    }
}
Log ""

# 10. Windows Event Log errors (last 10 from Application log)
Log "── 10. Windows Event Log (last errors from today) ──────────"
$events = Get-EventLog -LogName Application -EntryType Error -Newest 10 -ErrorAction SilentlyContinue |
          Where-Object { $_.Source -like "*Hotel*" -or $_.Source -like "*Sankalp*" -or $_.Source -like "*.NET*" -or $_.Source -like "*Application Error*" }
if ($events) {
    foreach ($e in $events) {
        Log "  [$($e.TimeGenerated)] $($e.Source): $($e.Message.Substring(0, [Math]::Min(200, $e.Message.Length)))"
    }
} else {
    Log "  No matching app errors in Event Log"
}
Log ""

Log "============================================================"
Log "  END OF REPORT"
Log "============================================================"

# Write report to Desktop
$report.ToString() | Set-Content -Path $ReportPath -Encoding UTF8
Write-Host ""
Write-Host "✅ Report saved to: $ReportPath" -ForegroundColor Green
Write-Host "📧 Please send this file to your support team." -ForegroundColor Cyan
Write-Host ""
Pause
