; Inno Setup script for DNSYar.
; Builds a Windows installer (Setup.exe) from a self-contained publish output.
;
; Usage (after `dotnet publish -r win-x64 -o Release\win-x64`):
;   ISCC.exe installer\DNSYar.iss /DMyAppVersion=0.7.1
;
; MyAppVersion defaults below if not passed on the command line.

#ifndef MyAppVersion
  #define MyAppVersion "0.0.0"
#endif

#define MyAppName "DNSYar"
#define MyAppPublisher "سهند رضوان"
#define MyAppURL "https://t.me/sahandse"
#define MyAppExeName "DNSYar.exe"
#define MyPublishDir "..\Release\win-x64"

[Setup]
AppId={{6F2B6C7E-6C0A-4A1E-9C0D-6F0C1D2E9A31}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=..\Release\installer
OutputBaseFilename=DNSYar-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64
; DNSYar changes the active network adapter's DNS servers, which requires elevation.
PrivilegesRequired=admin
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop icon"; GroupDescription: "Additional icons:"; Flags: unchecked

[Files]
Source: "{#MyPublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
; DNSYar.exe's manifest requests requireAdministrator. Launching it directly here would use
; CreateProcess, which does not honor that manifest and fails with error 740 ("requires
; elevation") even though Setup itself is elevated. Routing through explorer.exe makes Windows
; launch it the same way a shortcut double-click would, which correctly triggers the UAC prompt.
Filename: "{win}\explorer.exe"; Parameters: """{app}\{#MyAppExeName}"""; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent
