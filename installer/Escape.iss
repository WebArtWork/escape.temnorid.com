; Inno Setup script for the Windows installer: Temnorid-Escape-Setup.exe
; Build the player first (Unity: Temnorid > Escape > Build Windows Player), then compile this with Inno Setup:
;   "ISCC.exe" installer\Escape.iss
; The installer is written to Builds\Installer.

#define AppName "Temnorid Escape"
#define AppVersion "0.1.0"
#define AppExe "TemnoridEscape.exe"

[Setup]
AppId={{5D7B1E32-8A64-4C09-B3F1-2E9A6C4D8B15}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=Temnorid
AppPublisherURL=https://escape.temnorid.com
DefaultDirName={autopf}\Temnorid Escape
DefaultGroupName=Temnorid Escape
DisableProgramGroupPage=yes
OutputDir=..\Builds\Installer
OutputBaseFilename=Temnorid-Escape-Setup
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequiredOverridesAllowed=dialog
UninstallDisplayIcon={app}\{#AppExe}
WizardStyle=modern

[Files]
Source: "..\Builds\Windows\TemnoridEscape\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"

[Run]
Filename: "{app}\{#AppExe}"; Description: "Start {#AppName}"; Flags: nowait postinstall skipifsilent
