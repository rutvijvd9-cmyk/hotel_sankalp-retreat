# build_installer.ps1
# Complete build pipeline for Hotel Sankalp Retreat installer
$Root = $PSScriptRoot

function Step([string]$msg) {
    Write-Host ""
    Write-Host "==========================================================" -ForegroundColor Cyan
    Write-Host "  $msg" -ForegroundColor Cyan
    Write-Host "==========================================================" -ForegroundColor Cyan
}

# --- STEP 1: Generate hotel.ico ---
Step "Step 1/5 - Generating hotel.ico"
$iconScript = Join-Path $Root "Setup\create_icon.ps1"
$userPng = "C:\Users\ASUS\.gemini\antigravity\brain\8d192d3d-ee94-4f86-891d-c26c09a1fe13\media__1785069143711.png"
if (Test-Path $userPng) {
    & powershell -ExecutionPolicy Bypass -File $iconScript -SourcePngPath $userPng
    if ($LASTEXITCODE -ne 0) { throw "Icon generation failed." }
} else {
    Write-Host "Using existing hotel.ico in HotelAutomation.Worker" -ForegroundColor Yellow
}

# Copy hotel.ico to Setup folder for installer.iss
$setupIco = Join-Path $Root "Setup\hotel.ico"
$workerIco = Join-Path $Root "HotelAutomation.Worker\hotel.ico"
if (Test-Path $workerIco) {
    Copy-Item $workerIco $setupIco -Force
}

# --- STEP 2: Build React UI ---
Step "Step 2/5 - Building React UI (npm run build)"
Push-Location (Join-Path $Root "HotelAutomation.UI")
try {
    npm run build
    if ($LASTEXITCODE -ne 0) { throw "npm run build failed." }
} finally {
    Pop-Location
}

# --- STEP 3: Publish .NET backend ---
Step "Step 3/5 - Publishing .NET backend (self-contained, win-x64)"
Get-Process "HotelAutomation.Worker" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 500
$publishOut = Join-Path $Root "publish_output"
if (Test-Path $publishOut) { Remove-Item $publishOut -Recurse -Force -ErrorAction SilentlyContinue }

dotnet publish "$Root\HotelAutomation.Worker\HotelAutomation.Worker.csproj" `
    --configuration Release `
    --framework net10.0-windows10.0.19041.0 `
    --runtime win-x64 `
    --self-contained true `
    --output $publishOut

if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed." }

# Copy seed database and strip all template/rule data so the installer never
# ships stale template names.  On first run Program.cs copies this clean DB to
# AppData, and the startup TemplateSyncScheduler immediately pulls the live
# template list from Meta — so ExternalTemplateName is always correct.
$dbSource = Join-Path $Root "HotelAutomation.Worker\hotel_automation.db"
$dbDest   = Join-Path $publishOut "hotel_automation.db"
if (Test-Path $dbSource) {
    Copy-Item $dbSource $dbDest -Force
    Write-Host "Seed DB copied. Stripping MessageTemplates and AutomationRules rows..." -ForegroundColor Yellow

    # Use sqlite3.exe if available; fall back to a .NET inline script
    $sqlite3 = Get-Command sqlite3 -ErrorAction SilentlyContinue
    if ($sqlite3) {
        & sqlite3 $dbDest "DELETE FROM MessageTemplates; DELETE FROM AutomationRules; VACUUM;"
        if ($LASTEXITCODE -eq 0) {
            Write-Host "Seed DB cleaned via sqlite3." -ForegroundColor Green
        } else {
            Write-Warning "sqlite3 cleanup returned non-zero exit code. Seed DB may still contain stale data."
        }
    } else {
        # Inline .NET to clean the DB without requiring sqlite3 on PATH
        Add-Type -Path (Get-ChildItem "$publishOut\Microsoft.Data.Sqlite.dll" | Select-Object -ExpandProperty FullName -ErrorAction SilentlyContinue)
        try {
            $cs  = "Data Source=$dbDest"
            $con = New-Object Microsoft.Data.Sqlite.SqliteConnection($cs)
            $con.Open()
            foreach ($sql in @("DELETE FROM MessageTemplates;", "DELETE FROM AutomationRules;", "VACUUM;")) {
                $cmd = $con.CreateCommand(); $cmd.CommandText = $sql; $cmd.ExecuteNonQuery() | Out-Null
            }
            $con.Close()
            Write-Host "Seed DB cleaned via inline .NET SqliteConnection." -ForegroundColor Green
        } catch {
            Write-Warning "Could not clean seed DB automatically: $_. The startup TemplateSyncScheduler will repair stale names at runtime."
        }
    }
} else {
    Write-Host "No local hotel_automation.db found — installer will ship without a seed DB (fresh install)." -ForegroundColor Yellow
}

# --- STEP 4: Create & apply self-signed code-signing certificate ---
Step "Step 4/5 - Applying self-signed certificate"
$signScript = Join-Path $Root "Setup\sign_and_build.ps1"
& powershell -ExecutionPolicy Bypass -File $signScript
if ($LASTEXITCODE -ne 0) { throw "Signing or compilation failed." }

$outSetup = Join-Path $Root "installer_output\TheSankalpRetreat_Setup.exe"
$rootSetup = Join-Path $Root "HotelSankalpRetreat_Setup.exe"
if (Test-Path $outSetup) {
    Copy-Item $outSetup $rootSetup -Force
    Write-Host "Copied $outSetup to $rootSetup" -ForegroundColor Green
}

Write-Host ""
Write-Host "BUILD COMPLETE!" -ForegroundColor Green
