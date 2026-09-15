@echo off
echo [run.bat] Freeing port 8000 and stopping any previous worker...
for /f "tokens=5" %%a in ('netstat -aon 2^>nul ^| findstr ":8000" ^| findstr "LISTENING"') do (
    echo Terminating PID %%a on port 8000...
    taskkill /F /PID %%a >nul 2>&1
)
taskkill /F /IM HotelAutomation.Worker.exe >nul 2>&1
timeout /t 1 /nobreak >nul

echo Starting Worker (dotnet)...
start "Worker" cmd /c "dotnet run --project HotelAutomation.Worker\HotelAutomation.Worker.csproj"

echo Starting UI (Vite)...
start "UI" cmd /c "cd HotelAutomation.UI && npm run dev"

echo.
echo ========================================
echo   Worker API: http://localhost:8000
echo   UI (Vite):  http://localhost:5173
echo ========================================
echo.
echo Close the opened windows to stop services.
pause
