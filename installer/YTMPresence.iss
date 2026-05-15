#ifndef AppVersion
#define AppVersion "0.0.0"
#endif

#ifndef Runtime
#define Runtime "win-x64"
#endif

#ifndef AppSource
#define AppSource "artifacts\release\YTMPresence-0.0.0-win-x64-app"
#endif

#ifndef ExtensionSource
#define ExtensionSource "artifacts\release\extension"
#endif

#ifndef ReleaseSummary
#define ReleaseSummary "artifacts\release\RELEASE.txt"
#endif

#ifndef AppIcon
#define AppIcon "YTMPresence.Tray.Wpf\Assets\app.ico"
#endif

#define AppName "YTMPresence"
#define AppPublisher "YTMPresence"
#define AppUrl "https://github.com/Kentarohakase/YTMPresence"

[Setup]
AppId={{B83D5CB9-A9F4-49E6-9C2C-A206C44E7218}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}/issues
AppUpdatesURL={#AppUrl}/releases
DefaultDirName={localappdata}\Programs\YTMPresence
DefaultGroupName=YTMPresence
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
OutputBaseFilename=YTMPresence-{#AppVersion}-{#Runtime}-setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
SetupIconFile={#AppIcon}
UninstallDisplayIcon={app}\app\YTMPresence.exe
VersionInfoVersion={#AppVersion}
VersionInfoCompany={#AppPublisher}
VersionInfoDescription=YTMPresence Setup
VersionInfoProductName={#AppName}
VersionInfoProductVersion={#AppVersion}

[InstallDelete]
Type: filesandordirs; Name: "{app}\app"
Type: filesandordirs; Name: "{app}\extension"
Type: files; Name: "{app}\RELEASE.txt"
Type: files; Name: "{app}\install.json"
Type: files; Name: "{app}\uninstall.ps1"

[UninstallDelete]
Type: filesandordirs; Name: "{app}\app"
Type: filesandordirs; Name: "{app}\extension"
Type: files; Name: "{app}\RELEASE.txt"
Type: files; Name: "{app}\install.json"
Type: files; Name: "{app}\uninstall.ps1"
Type: dirifempty; Name: "{app}"

[Files]
Source: "{#AppSource}\*"; DestDir: "{app}\app"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#ExtensionSource}\*"; DestDir: "{app}\extension"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#ReleaseSummary}"; DestDir: "{app}"; DestName: "RELEASE.txt"; Flags: ignoreversion

[Icons]
Name: "{group}\YTMPresence"; Filename: "{app}\app\YTMPresence.exe"; WorkingDir: "{app}\app"; IconFilename: "{app}\app\YTMPresence.exe"
Name: "{group}\YTMPresence deinstallieren"; Filename: "{uninstallexe}"

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Uninstall\YTMPresence"; Flags: deletekey

[UninstallRun]
Filename: "powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -Command ""Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'YTM Presence' -ErrorAction SilentlyContinue; Remove-Item -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\YTMPresence' -Recurse -Force -ErrorAction SilentlyContinue"""; Flags: runhidden

[Run]
Filename: "{app}\app\YTMPresence.exe"; Description: "YTMPresence starten"; WorkingDir: "{app}\app"; Flags: nowait postinstall skipifsilent

[Code]
procedure StopYTMPresence();
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{cmd}'), '/C taskkill /IM YTMPresence.exe /F /T >NUL 2>NUL', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  StopYTMPresence();
  Result := '';
end;

function InitializeUninstall(): Boolean;
begin
  StopYTMPresence();
  Result := True;
end;
