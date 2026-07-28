# trust_on_this_machine.ps1
# Run this script ONCE on each hotel computer to trust the self-signed certificate.
# This prevents Windows SmartScreen from warning about the installer.
#
# Usage (Run as Administrator):
#   powershell -ExecutionPolicy Bypass -File trust_on_this_machine.ps1
#
# After running this, double-clicking HotelSankalpRetreat_Setup.exe will install without any warning.

param(
    [string]$CertPath = "$PSScriptRoot\HotelSankalp_CodeSign.cer"
)

if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Host "ERROR: Please run this script as Administrator." -ForegroundColor Red
    Write-Host "Right-click the script and choose 'Run as administrator'." -ForegroundColor Yellow
    pause
    exit 1
}

if (-not (Test-Path $CertPath)) {
    Write-Host "ERROR: Certificate not found at: $CertPath" -ForegroundColor Red
    Write-Host "Make sure HotelSankalp_CodeSign.cer is in the same folder as this script." -ForegroundColor Yellow
    pause
    exit 1
}

Write-Host "Installing Hotel Sankalp code-signing certificate as trusted..." -ForegroundColor Cyan

# Import to Trusted Root CA store (suppresses SmartScreen for signed files)
Import-Certificate -FilePath $CertPath -CertStoreLocation Cert:\LocalMachine\Root | Out-Null

# Import to Trusted Publishers store (suppresses "Unknown Publisher" UAC dialog)
Import-Certificate -FilePath $CertPath -CertStoreLocation Cert:\LocalMachine\TrustedPublisher | Out-Null

Write-Host "Done! This machine will no longer show security warnings for Hotel Sankalp software." -ForegroundColor Green
pause
