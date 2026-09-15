# Free port 8000 and stop any previous worker instances
Write-Host "Freeing port 8000 and stopping existing worker..." -ForegroundColor Yellow
Get-Process -Name "HotelAutomation.Worker" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
try {
    $connections = Get-NetTCPConnection -LocalPort 8000 -State Listen -ErrorAction SilentlyContinue
    foreach ($conn in $connections) {
        if ($conn.OwningProcess -gt 0) {
            Write-Host "Terminating process $($conn.OwningProcess) listening on port 8000..." -ForegroundColor Yellow
            Stop-Process -Id $conn.OwningProcess -Force -ErrorAction SilentlyContinue
        }
    }
} catch {}
Start-Sleep -Milliseconds 800

# Start HotelAutomation Worker (Backend API + Background Services)
Write-Host "Starting Worker (dotnet)..." -ForegroundColor Cyan
$worker = Start-Process -FilePath "dotnet" -ArgumentList "run --project HotelAutomation.Worker\HotelAutomation.Worker.csproj" -WorkingDirectory $PSScriptRoot -NoNewWindow -PassThru

# Start React UI (Vite Dev Server)
Write-Host "Starting UI (Vite)..." -ForegroundColor Cyan
$ui = Start-Process -FilePath "npm" -ArgumentList "run dev" -WorkingDirectory (Join-Path $PSScriptRoot "HotelAutomation.UI") -NoNewWindow -PassThru

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  Worker API: http://localhost:8000" -ForegroundColor Yellow
Write-Host "  UI (Vite):  http://localhost:5173" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Press ANY KEY to stop all services..." -ForegroundColor Magenta
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyUp")

Write-Host "Stopping services..." -ForegroundColor Cyan
$worker.Kill()
$ui.Kill()
Write-Host "Done." -ForegroundColor Green
