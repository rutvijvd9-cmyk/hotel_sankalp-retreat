@echo off
echo Starting Worker (dotnet)...
start "Worker" cmd /c "dotnet run --project HotelAutomation.Worker\HotelAutomation.Worker.csproj"

echo Starting UI (Vite)...
start "UI" cmd /c "cd HotelAutomation.UI && npm run dev"

echo.
echo ========================================
echo   Worker API: http://localhost:59725
echo   UI (Vite):  http://localhost:5173
echo ========================================
echo.
echo Close the opened windows to stop services.
pause
