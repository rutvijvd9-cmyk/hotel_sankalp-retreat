; ─────────────────────────────────────────────────────────────────────────────
; Inno Setup Script — Hotel Sankalp Retreat Automation
; Produces: Scalifts_Setup.exe
;
; Requirements:
;   - Inno Setup 6 installed: https://jrsoftware.org/isdl.php
;   - Run build_installer.ps1 first to populate the publish_output\ folder
; ─────────────────────────────────────────────────────────────────────────────

#define AppName      "Scalifts"
#define AppVersion   "1.0"
#define AppPublisher "Scalifts"
#define AppURL       "http://localhost:8000"
#define AppExe       "HotelAutomation.Worker.exe"

[Setup]
AppId={{F3A92C1D-7B4E-4D8F-A3C6-2E9B1F0D5A72}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}
AppUpdatesURL={#AppURL}
DefaultDirName={autopf}\Scalifts
DefaultGroupName={#AppName}
AllowNoIcons=no
; Output installer details
OutputDir=..\installer_output
OutputBaseFilename=Scalifts_Setup
; Icon for the installer exe itself
SetupIconFile=..\HotelAutomation.Worker\hotel.ico
; Compression
Compression=lzma2/ultra64
SolidCompression=yes
; Modern wizard
WizardStyle=modern
WizardSizePercent=120
; Uninstaller
UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\{#AppExe}
; Minimum Windows 10
MinVersion=10.0.17763
; Require admin for Program Files install
PrivilegesRequired=admin
; Prevent running installer while app is open
CloseApplications=yes
CloseApplicationsFilter=*HotelAutomation.Worker.exe*
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
; Desktop shortcut — checked by default
Name: "desktopicon"; Description: "Create a &Desktop shortcut"; GroupDescription: "Additional icons:"; Flags: checkedonce
; Run at Windows startup
Name: "startup"; Description: "Start automatically when &Windows starts"; GroupDescription: "Startup:"; Flags: unchecked

[Files]
; All published output files → installed to {app}
Source: "..\publish_output\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; Schema.sql — used by the app to initialise the SQLite database on first launch
Source: "..\HotelAutomation.Data\Schema.sql"; DestDir: "{app}"; Flags: ignoreversion
; Printer installer script — run by [Run] section below with admin rights
Source: "install_printer.ps1"; DestDir: "{app}"; Flags: ignoreversion

[Dirs]
; Create the PDF watch folder and processed archive folder next to the exe
; Hotel staff drop/print bills to {app}\PDFs, processed copies go to {app}\PDFs\Processed
Name: "{app}\PDFs";           Permissions: users-modify; Flags: uninsneveruninstall
Name: "{app}\PDFs\Processed"; Permissions: users-modify; Flags: uninsneveruninstall


[Icons]
; Start Menu group
Name: "{group}\{#AppName}";                  Filename: "{app}\{#AppExe}"
Name: "{group}\Uninstall {#AppName}";        Filename: "{uninstallexe}"

; Desktop shortcut (only when task selected)
Name: "{autodesktop}\{#AppName}";            Filename: "{app}\{#AppExe}"; Tasks: desktopicon

; Windows startup entry (only when task selected) — use commonstartup (all-users) since installer runs as admin
Name: "{commonstartup}\{#AppName}";          Filename: "{app}\{#AppExe}"; Tasks: startup

[Registry]
; Store install path for reference
Root: HKLM; Subkey: "Software\HotelSankalpRetreat"; ValueType: string; ValueName: "InstallDir"; ValueData: "{app}"; Flags: uninsdeletekey

[Run]
; 1. Install the Scalifts virtual printer (runs as admin — same as the installer)
Filename: "powershell.exe"; \
    Parameters: "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File ""{app}\install_printer.ps1"" -InstallDir ""{app}"""; \
    Flags: runhidden waituntilterminated; \
    StatusMsg: "Installing Scalifts printer..."

; 2. Offer to launch the app after installation
Filename: "{app}\{#AppExe}"; \
    Description: "Launch {#AppName} now"; \
    Flags: nowait postinstall skipifsilent

[UninstallRun]
; Kill the running process before uninstalling
Filename: "taskkill.exe"; Parameters: "/F /IM HotelAutomation.Worker.exe"; Flags: runhidden; RunOnceId: "KillApp"
; Remove the Scalifts virtual printer and its port on uninstall
Filename: "powershell.exe"; Parameters: "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command ""Remove-Printer -Name 'Scalifts' -ErrorAction SilentlyContinue; Get-PrinterPort | Where-Object {{ $_.Name -like '*sankalp_bill*' }} | ForEach-Object {{ Remove-PrinterPort -Name $_.Name -ErrorAction SilentlyContinue }}"""; Flags: runhidden; RunOnceId: "RemovePrinter"

[Code]
// Run at installer start: kill running app and clean up old printers/ports
function InitializeSetup(): Boolean;
var
  ResultCode: Integer;
begin
  // 1. Kill any running instance
  Exec('taskkill.exe', '/F /IM HotelAutomation.Worker.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);

  // 2. Remove old "Hotel Sankalp Printer" if it exists (from previous installs)
  Exec('powershell.exe',
    '-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command ' +
    '"Remove-Printer -Name ''Hotel Sankalp Printer'' -ErrorAction SilentlyContinue"',
    '', SW_HIDE, ewWaitUntilTerminated, ResultCode);

  // 3. Remove old "Scalifts" printer if already installed (will be reinstalled by app)
  Exec('powershell.exe',
    '-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command ' +
    '"Remove-Printer -Name ''Scalifts'' -ErrorAction SilentlyContinue"',
    '', SW_HIDE, ewWaitUntilTerminated, ResultCode);

  // 4. Remove any leftover local ports pointing to old HotelPrints paths
  Exec('powershell.exe',
    '-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command ' +
    '"Get-PrinterPort | Where-Object { $_.Name -like ''*HotelPrints*'' -or $_.Name -like ''*sankalp_bill*'' } | ForEach-Object { Remove-PrinterPort -Name $_.Name -ErrorAction SilentlyContinue }"',
    '', SW_HIDE, ewWaitUntilTerminated, ResultCode);

  Result := True;
end;
