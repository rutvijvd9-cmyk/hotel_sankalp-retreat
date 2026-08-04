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
& powershell -ExecutionPolicy Bypass -File $iconScript -SourcePngPath $userPng
if ($LASTEXITCODE -ne 0) { throw "Icon generation failed." }

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
$publishOut = Join-Path $Root "publish_output"
if (Test-Path $publishOut) { Remove-Item $publishOut -Recurse -Force }

dotnet publish "$Root\HotelAutomation.Worker\HotelAutomation.Worker.csproj" `
    --configuration Release `
    --framework net10.0-windows10.0.19041.0 `
    --runtime win-x64 `
    --self-contained true `
    --output $publishOut

if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed." }

# Copy database if needed
$dbSource = Join-Path $Root "HotelAutomation.Worker\hotel_automation.db"
$dbDest   = Join-Path $publishOut "hotel_automation.db"
if ((Test-Path $dbSource) -and -not (Test-Path $dbDest)) {
    Copy-Item $dbSource $dbDest
}

# --- STEP 4: Create & apply self-signed code-signing certificate ---
Step "Step 4/5 - Applying self-signed certificate"
$signScript = Join-Path $Root "Setup\sign_and_build.ps1"
& powershell -ExecutionPolicy Bypass -File $signScript
if ($LASTEXITCODE -ne 0) { throw "Signing or compilation failed." }

Write-Host ""
Write-Host "BUILD COMPLETE!" -ForegroundColor Green
