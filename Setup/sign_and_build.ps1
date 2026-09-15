# sign_and_build.ps1 - Creates cert, signs exe, compiles installer
$Root         = Split-Path -Parent $PSScriptRoot
$CertName     = "Hotel Sankalp Retreat"
$CertStore    = "Cert:\CurrentUser\My"
$CerExport    = "$Root\Setup\HotelSankalp_CodeSign.cer"
$PfxExport    = "$Root\Setup\HotelSankalp_CodeSign.pfx"
$PfxPass      = ConvertTo-SecureString -String "HotelSankalp2025!" -Force -AsPlainText
$PublishDir   = "$Root\publish_output"
$OutDir       = "$Root\installer_output"
$ExeToSign    = "$PublishDir\HotelAutomation.Worker.exe"
$InstallerExe = "$OutDir\TheSankalpRetreat_Setup.exe"

# --- Step 1: Create self-signed cert -----------------------------------------
Write-Host ""
Write-Host "[1/4] Creating self-signed code-signing certificate..." -ForegroundColor Cyan

# Remove old matching certs
Get-ChildItem $CertStore -ErrorAction SilentlyContinue |
    Where-Object { $_.Subject -like "*$CertName*" } |
    Remove-Item -Force -ErrorAction SilentlyContinue

$Cert = New-SelfSignedCertificate `
    -Type CodeSigningCert `
    -Subject "CN=$CertName, O=Hotel Sankalp Retreat, C=IN" `
    -KeyAlgorithm RSA `
    -KeyLength 4096 `
    -HashAlgorithm SHA256 `
    -CertStoreLocation $CertStore `
    -NotAfter (Get-Date).AddYears(10)

Write-Host "  Thumbprint: $($Cert.Thumbprint)" -ForegroundColor Green

Export-Certificate -Cert $Cert -FilePath $CerExport -Type CERT | Out-Null
Write-Host "  .cer exported: $CerExport" -ForegroundColor Green

Export-PfxCertificate -Cert $Cert -FilePath $PfxExport -Password $PfxPass | Out-Null
Write-Host "  .pfx exported: $PfxExport" -ForegroundColor Green

# --- Step 2: Find signtool.exe -----------------------------------------------
Write-Host ""
Write-Host "[2/4] Locating signtool.exe..." -ForegroundColor Cyan

$Signtool = $null
if (Test-Path "C:\Program Files (x86)\Windows Kits") {
    $Signtool = Get-ChildItem "C:\Program Files (x86)\Windows Kits" `
        -Filter "signtool.exe" -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -like "*x64*" } |
        Sort-Object FullName -Descending |
        Select-Object -First 1 -ExpandProperty FullName
}

if ($null -ne $Signtool) {
    Write-Host "  Found: $Signtool" -ForegroundColor Green

    Write-Host "  Signing $ExeToSign ..." -ForegroundColor Yellow
    & $Signtool sign /fd SHA256 /f $PfxExport /p "HotelSankalp2025!" /v $ExeToSign
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  Exe signed OK" -ForegroundColor Green
    } else {
        Write-Host "  WARNING: Exe signing returned non-zero (non-fatal)" -ForegroundColor Yellow
    }
} else {
    Write-Host "  signtool.exe not found - skipping exe signing." -ForegroundColor Yellow
}

# --- Step 3: Compile Inno Setup installer ------------------------------------
Write-Host ""
Write-Host "[3/4] Compiling Inno Setup installer..." -ForegroundColor Cyan

$possibleIscc = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe",
    "C:\Users\ASUS\AppData\Local\Programs\Inno Setup 6\ISCC.exe"
)
$ISCC = $possibleIscc | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $ISCC) {
    Write-Host "  ERROR: Inno Setup 6 (ISCC.exe) not found. Please install Inno Setup 6 from https://jrsoftware.org/isdl.php" -ForegroundColor Red
    exit 1
}

if (-not (Test-Path $OutDir)) {
    New-Item -ItemType Directory $OutDir | Out-Null
}

& $ISCC "$Root\Setup\installer.iss"
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: ISCC compilation failed." -ForegroundColor Red
    exit 1
}
Write-Host "  Installer compiled: $InstallerExe" -ForegroundColor Green

# --- Step 4: Sign the installer ----------------------------------------------
if (($null -ne $Signtool) -and (Test-Path $InstallerExe)) {
    Write-Host ""
    Write-Host "[4/4] Signing installer..." -ForegroundColor Cyan
    & $Signtool sign /fd SHA256 /f $PfxExport /p "HotelSankalp2025!" /v $InstallerExe
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  Installer signed OK" -ForegroundColor Green
    }
} else {
    Write-Host "[4/4] Skipping installer signing (signtool not available)." -ForegroundColor Yellow
}

# --- Done --------------------------------------------------------------------
Write-Host ""
Write-Host "==========================================================" -ForegroundColor Green
Write-Host "  ALL DONE!" -ForegroundColor Green
Write-Host "  Installer : $InstallerExe" -ForegroundColor Yellow
Write-Host "  Cert      : $CerExport" -ForegroundColor Yellow
Write-Host "==========================================================" -ForegroundColor Green
Write-Host ""
Write-Host "TO USE ON THIS MACHINE (no SmartScreen warning):" -ForegroundColor Cyan
Write-Host "  1. Run Setup\trust_on_this_machine.ps1 as Administrator" -ForegroundColor White
Write-Host "  2. Then double-click TheSankalpRetreat_Setup.exe" -ForegroundColor White
