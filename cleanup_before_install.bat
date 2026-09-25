@echo off
:: ============================================================================
:: The Sankalp Retreat - Pre-Installation & Reset Cleanup Tool
:: Run this as Administrator to cleanly remove stuck tasks, processes, and ports
:: ============================================================================
echo [1/4] Stopping any active Hotel Automation processes...
taskkill /F /IM HotelAutomation.Worker.exe /T >nul 2>&1

echo [2/4] Clearing port 8001 and 8000 if occupied...
powershell -NoProfile -ExecutionPolicy Bypass -Command "Get-NetTCPConnection -LocalPort 8001,8000 -ErrorAction SilentlyContinue | ForEach-Object { Stop-Process -Id $_.OwningProcess -Force -ErrorAction SilentlyContinue }" >nul 2>&1

echo [3/4] Removing lingering Windows Scheduled Tasks (watchdogs)...
schtasks /Delete /TN "TheSankalpRetreat\TheSankalpRetreatWatchdog" /F >nul 2>&1
schtasks /Delete /TN "TheSankalpRetreat\TheSankalpRetreatAutoStart" /F >nul 2>&1
schtasks /Delete /TN "Scalifts\ScaliftsWatchdog" /F >nul 2>&1

echo [4/4] Clearing stale SQLite lock files...
powershell -NoProfile -ExecutionPolicy Bypass -Command "Get-ChildItem -Path @('C:\Program Files\TheSankalpRetreat', 'C:\Program Files\The Sankalp Retreat', '.') -Filter 'hotel_automation.db-*' -Recurse -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue" >nul 2>&1

echo.
echo ============================================================================
echo Clean up complete! The machine is now completely clear.
echo You can now install or run the new HotelAutomation.Worker.exe.
echo ============================================================================
pause
