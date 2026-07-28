# install_printer.ps1
# Installs the "Scalifts" virtual printer using the built-in Microsoft Print To PDF driver.
# This script is run by the installer with admin privileges.
# Usage: .\install_printer.ps1 -InstallDir "C:\Program Files (x86)\Scalifts"

param(
    [string]$InstallDir = (Split-Path -Parent $MyInvocation.MyCommand.Path)
)

$ErrorActionPreference = 'Stop'

$PrinterName = "Scalifts"
$DriverName  = "Microsoft Print To PDF"
$PdfDir      = Join-Path $InstallDir "PDFs"
$PortPath    = Join-Path $PdfDir "sankalp_bill.pdf"

Write-Host "Scalifts Printer Installer"
Write-Host "  Install Dir : $InstallDir"
Write-Host "  PDF Folder  : $PdfDir"
Write-Host "  Port (file) : $PortPath"

# 1. Ensure PDFs directory exists
if (-not (Test-Path $PdfDir)) {
    New-Item -ItemType Directory -Force -Path $PdfDir | Out-Null
    Write-Host "Created PDFs folder: $PdfDir"
}

# Grant modify permissions to Everyone and Users so Print Spooler / local processes can write to this directory
icacls "$PdfDir" /grant "Everyone:(OI)(CI)M" /T
icacls "$PdfDir" /grant "Users:(OI)(CI)M" /T


# 2. Create placeholder file so the port can reference it
if (-not (Test-Path $PortPath)) {
    New-Item -ItemType File -Force -Path $PortPath | Out-Null
    Write-Host "Created placeholder file: $PortPath"
}
icacls "$PortPath" /grant "Everyone:M"
icacls "$PortPath" /grant "Users:M"


# 3. Remove any existing Scalifts printer (clean slate)
$existing = Get-Printer -Name $PrinterName -ErrorAction SilentlyContinue
if ($existing) {
    Remove-Printer -Name $PrinterName -ErrorAction SilentlyContinue
    Write-Host "Removed existing printer."
}

# 4. Check if the port already exists
$existingPort = Get-PrinterPort -Name $PortPath -ErrorAction SilentlyContinue
if (-not $existingPort) {
    # 5. Create local port (port name = file path = no save dialog!)
    Add-PrinterPort -Name $PortPath
    Write-Host "Created printer port: $PortPath"
} else {
    Write-Host "Printer port already exists: $PortPath"
}

# 6. Install the printer
Add-Printer -Name $PrinterName -DriverName $DriverName -PortName $PortPath -Comment "Scalifts - prints bills directly to PDF"
Write-Host "SUCCESS: Scalifts printer installed!"
