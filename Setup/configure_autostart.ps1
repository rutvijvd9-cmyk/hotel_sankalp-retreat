# configure_autostart.ps1
# Called by the Inno Setup installer [Run] section after installation.
# Registers auto-start in the interactive user's desktop session so that:
#   1. The Tray Icon appears in the Windows system tray / taskbar
#   2. Desktop notifications (bill printed, WhatsApp alerts) are visible
#   3. The app restarts automatically on failure
#
# Runs in the active user desktop session (not isolated Session 0 SYSTEM).

param(
    [string]$InstallDir = "$env:ProgramFiles\TheSankalpRetreat"
)

$ExePath = Join-Path $InstallDir "HotelAutomation.Worker.exe"
$TaskFolder = "TheSankalpRetreat"

# ── 1. Clean up legacy SYSTEM tasks so they never run in Session 0 ──────────────
try {
    Unregister-ScheduledTask -TaskPath "\TheSankalpRetreat\" -TaskName "TheSankalpRetreatWatchdog" -Confirm:$false -ErrorAction SilentlyContinue
    Unregister-ScheduledTask -TaskPath "\TheSankalpRetreat\" -TaskName "TheSankalpRetreatAutoStart" -Confirm:$false -ErrorAction SilentlyContinue
    Unregister-ScheduledTask -TaskName "TheSankalpRetreatWatchdog" -Confirm:$false -ErrorAction SilentlyContinue
    Unregister-ScheduledTask -TaskName "TheSankalpRetreatAutoStart" -Confirm:$false -ErrorAction SilentlyContinue
} catch { }

# ── 2. Helper: ensure task folder exists ────────────────────────────────────────
$scheduler = New-Object -ComObject Schedule.Service
$scheduler.Connect()
try {
    $scheduler.GetFolder("\$TaskFolder") | Out-Null
} catch {
    $root = $scheduler.GetFolder("\")
    $root.CreateFolder($TaskFolder) | Out-Null
}

# ── Clean up any legacy shell startup shortcuts so Task Scheduler remains sole trigger
try {
    $commonStartup = [Environment]::GetFolderPath('CommonStartup')
    $userStartup = [Environment]::GetFolderPath('Startup')
    Remove-Item (Join-Path $commonStartup "TheSankalpRetreat.lnk") -Force -ErrorAction SilentlyContinue
    Remove-Item (Join-Path $userStartup "TheSankalpRetreat.lnk") -Force -ErrorAction SilentlyContinue
} catch { }

# ── 3. Task: TheSankalpRetreatAutoStart — run at user logon in interactive session
$action1 = New-ScheduledTaskAction -Execute "`"$ExePath`"" -WorkingDirectory $InstallDir
# 15s delay ensures Explorer desktop and network stack are fully initialized after Windows updates/reboots
$trigger1 = New-ScheduledTaskTrigger -AtLogOn -UserRandomDelay (New-TimeSpan -Seconds 15)
$settings1 = New-ScheduledTaskSettingsSet `
    -MultipleInstances IgnoreNew `
    -RestartCount 3 `
    -RestartInterval (New-TimeSpan -Minutes 1) `
    -ExecutionTimeLimit ([TimeSpan]::Zero)

# GroupId "BUILTIN\Users" ensures the task runs in the active desktop session of whichever user logs on
$principal = New-ScheduledTaskPrincipal -GroupId "BUILTIN\Users" -RunLevel Highest

Register-ScheduledTask `
    -TaskName "$TaskFolder\TheSankalpRetreatAutoStart" `
    -Action   $action1 `
    -Trigger  $trigger1 `
    -Settings $settings1 `
    -Principal $principal `
    -Force | Out-Null

Write-Host "TheSankalpRetreatAutoStart registered for interactive user logon." -ForegroundColor Green
Write-Host "Auto-start configured successfully." -ForegroundColor Cyan
