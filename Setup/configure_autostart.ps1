# configure_autostart.ps1
# Called by the Inno Setup installer [Run] section after installation.
# Registers two scheduled tasks:
#   1. TheSankalpRetreatAutoStart  — launches the app when Windows boots
#   2. TheSankalpRetreatWatchdog   — checks every 2 minutes and restarts the app if it has crashed
#
# Both tasks run as SYSTEM (highest privilege) so no user needs to be logged in.

param(
    [string]$InstallDir = "$env:ProgramFiles\TheSankalpRetreat"
)

$ExePath = Join-Path $InstallDir "HotelAutomation.Worker.exe"
$TaskFolder = "TheSankalpRetreat"

# ── Helper: ensure task folder exists ─────────────────────────────────────────
$scheduler = New-Object -ComObject Schedule.Service
$scheduler.Connect()
try {
    $scheduler.GetFolder("\$TaskFolder") | Out-Null
} catch {
    $root = $scheduler.GetFolder("\")
    $root.CreateFolder($TaskFolder) | Out-Null
}

# ── Task 1: TheSankalpRetreatAutoStart — run at system startup ─────────────────────────
$action1 = New-ScheduledTaskAction -Execute "`"$ExePath`"" -WorkingDirectory $InstallDir
$trigger1 = New-ScheduledTaskTrigger -AtStartup
$settings1 = New-ScheduledTaskSettingsSet `
    -MultipleInstances IgnoreNew `
    -RestartCount 3 `
    -RestartInterval (New-TimeSpan -Minutes 1) `
    -ExecutionTimeLimit ([TimeSpan]::Zero)   # no timeout

Register-ScheduledTask `
    -TaskName "$TaskFolder\TheSankalpRetreatAutoStart" `
    -Action   $action1 `
    -Trigger  $trigger1 `
    -Settings $settings1 `
    -RunLevel Highest `
    -User     "SYSTEM" `
    -Force | Out-Null

Write-Host "TheSankalpRetreatAutoStart task registered." -ForegroundColor Green

# ── Task 2: TheSankalpRetreatWatchdog — check every 5 minutes, restart if truly dead ─────────
$watchdogScript = @"
`$exePath = '$ExePath'
`$workDir = '$InstallDir'

`$proc = Get-Process -Name 'HotelAutomation.Worker' -ErrorAction SilentlyContinue
if (-not `$proc) {
    # Process is completely dead -> launch it
    Start-Process -FilePath `"`$exePath`" -WorkingDirectory `"`$workDir`"
    Write-EventLog -LogName Application -Source 'TheSankalpRetreat' -EventId 1001 -EntryType Information -Message 'TheSankalpRetreatWatchdog: restarted HotelAutomation.Worker.exe' -ErrorAction SilentlyContinue
} else {
    # Process exists -> verify /ping health (give 5s timeout)
    try {
        `$res = Invoke-RestMethod -Uri 'http://127.0.0.1:8000/ping' -TimeoutSec 5 -ErrorAction Stop
    } catch {
        # If /ping fails, sleep 10s and test again to prevent killing a process that is just busy or starting up
        Start-Sleep -Seconds 10
        try {
            `$res = Invoke-RestMethod -Uri 'http://127.0.0.1:8000/ping' -TimeoutSec 5 -ErrorAction Stop
        } catch {
            # Truly hung -> kill and restart
            Stop-Process -Name 'HotelAutomation.Worker' -Force -ErrorAction SilentlyContinue
            Start-Sleep -Seconds 3
            Start-Process -FilePath `"`$exePath`" -WorkingDirectory `"`$workDir`"
            Write-EventLog -LogName Application -Source 'TheSankalpRetreat' -EventId 1002 -EntryType Warning -Message 'TheSankalpRetreatWatchdog: process hung, restarted HotelAutomation.Worker.exe' -ErrorAction SilentlyContinue
        }
    }
}
"@

# Write the watchdog script next to the exe so it can be updated easily
$watchdogPath = Join-Path $InstallDir "watchdog.ps1"
$watchdogScript | Set-Content -Path $watchdogPath -Encoding UTF8

$action2 = New-ScheduledTaskAction `
    -Execute "powershell.exe" `
    -Argument "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File `"$watchdogPath`"" `
    -WorkingDirectory $InstallDir

# Repeat every 15 minutes, starting now, indefinitely
$trigger2 = New-ScheduledTaskTrigger -RepetitionInterval (New-TimeSpan -Minutes 15) -Once -At (Get-Date)

$settings2 = New-ScheduledTaskSettingsSet `
    -MultipleInstances IgnoreNew `
    -ExecutionTimeLimit (New-TimeSpan -Minutes 5)

Register-ScheduledTask `
    -TaskName "$TaskFolder\TheSankalpRetreatWatchdog" `
    -Action   $action2 `
    -Trigger  $trigger2 `
    -Settings $settings2 `
    -RunLevel Highest `
    -User     "SYSTEM" `
    -Force | Out-Null

Write-Host "TheSankalpRetreatWatchdog task registered (smart health checks every 15 minutes)." -ForegroundColor Green
Write-Host "Auto-start and crash-recovery configured successfully." -ForegroundColor Cyan
