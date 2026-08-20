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

# ── Task 2: TheSankalpRetreatWatchdog — check every 2 minutes, restart if dead ─────────
$watchdogScript = @"
`$exePath = '$ExePath'
`$workDir = '$InstallDir'
if (-not (Get-Process -Name 'HotelAutomation.Worker' -ErrorAction SilentlyContinue)) {
    Start-Process -FilePath `"`$exePath`" -WorkingDirectory `"`$workDir`"
    Write-EventLog -LogName Application -Source 'TheSankalpRetreat' -EventId 1001 -EntryType Information -Message 'TheSankalpRetreatWatchdog: restarted HotelAutomation.Worker.exe' -ErrorAction SilentlyContinue
}
"@

# Write the watchdog script next to the exe so it can be updated easily
$watchdogPath = Join-Path $InstallDir "watchdog.ps1"
$watchdogScript | Set-Content -Path $watchdogPath -Encoding UTF8

$action2 = New-ScheduledTaskAction `
    -Execute "powershell.exe" `
    -Argument "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File `"$watchdogPath`"" `
    -WorkingDirectory $InstallDir

# Repeat every 2 minutes, starting now, indefinitely
$trigger2 = New-ScheduledTaskTrigger -RepetitionInterval (New-TimeSpan -Minutes 2) -Once -At (Get-Date)

$settings2 = New-ScheduledTaskSettingsSet `
    -MultipleInstances IgnoreNew `
    -ExecutionTimeLimit (New-TimeSpan -Minutes 1)

Register-ScheduledTask `
    -TaskName "$TaskFolder\TheSankalpRetreatWatchdog" `
    -Action   $action2 `
    -Trigger  $trigger2 `
    -Settings $settings2 `
    -RunLevel Highest `
    -User     "SYSTEM" `
    -Force | Out-Null

Write-Host "TheSankalpRetreatWatchdog task registered (checks every 2 minutes)." -ForegroundColor Green
Write-Host "Auto-start and crash-recovery configured successfully." -ForegroundColor Cyan
