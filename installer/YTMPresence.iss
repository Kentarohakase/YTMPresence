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
; Rückfallebene über den Windows Restart Manager: schließt ausschließlich
; Prozesse, die Dateien unter {app} tatsächlich geöffnet halten – nie fremde
; oder portable Prozesse, die nur denselben Dateinamen tragen.
CloseApplications=force
RestartApplications=no
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
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "YTM Presence"; Flags: uninsdeletevalue

[Run]
Filename: "{app}\app\YTMPresence.exe"; Description: "YTMPresence starten"; WorkingDir: "{app}\app"; Flags: nowait postinstall skipifsilent

[Code]
// Beenden der laufenden installierten App – in dieser Reihenfolge:
//
// 1. Höfliche Aufforderung: Ein pfadgebundenes Named Event
//    (Local\YTMPresence.Shutdown.<schlüssel>) signalisiert genau der Instanz,
//    die aus dem Zielverzeichnis (app-Unterordner) läuft, sich regulär zu
//    beenden. Danach wird auf das Verschwinden des zugehörigen Mutex
//    (Local\YTMPresence.Running.<schlüssel>) gewartet.
// 2. Rückfallebene: CloseApplications=force (Windows Restart Manager)
//    schließt nur Prozesse, die Dateien im Zielverzeichnis geöffnet halten.
//
// Es wird bewusst kein taskkill nach Prozessnamen verwendet: Eine portable
// Kopie in einem anderen Ordner oder ein fremder Prozess mit demselben
// Dateinamen darf nicht beendet werden.
//
// Die Schlüsselberechnung muss exakt InstallerCoordination.GetInstanceKey in
// YTMPresence.Core entsprechen (ASCII-Lowercase, SHA256 über UTF-16LE).

const
  EVENT_MODIFY_STATE = $0002;
  ShutdownWaitTotalMs = 10000;
  ShutdownPollMs = 250;

function OpenEventW(dwDesiredAccess: DWORD; bInheritHandle: BOOL; lpName: String): THandle;
  external 'OpenEventW@kernel32.dll stdcall';
function SetEvent(hEvent: THandle): BOOL;
  external 'SetEvent@kernel32.dll stdcall';
function CloseHandle(hObject: THandle): BOOL;
  external 'CloseHandle@kernel32.dll stdcall';

{ ACHTUNG: Zweitimplementierung von InstallerCoordination.GetInstanceKey
  (YTMPresence.Core). Beide Seiten muessen bitgleich rechnen, sonst findet der
  Installer das Shutdown-Event der laufenden Instanz nicht mehr und faellt
  still auf den Windows Restart Manager zurueck - der taskkill-Ersatz waere
  damit wirkungslos, ohne dass irgendetwas fehlschlaegt.

  Gemeinsamer Testvektor (gepinnt in InstallerCoordinationTests):
    C:\Users\Kenta\AppData\Local\Programs\YTMPresence\app
    -> 229237f93d35110457aaa7122614dcabd493e49146361ddeac57b0cd3aa5d13d

  Der Unit-Test sichert nur die C#-Seite. Aenderungen hier bleiben unbemerkt,
  bis der Update-Test aus docs/RELEASE_QA.md sie sichtbar macht: Wenn sich
  YTMPresence nach 'Installieren' nicht selbst beendet, sondern erst der
  Restart Manager greift, sind die Schluessel auseinandergelaufen. }
function MakeInstanceKey(Path: String): String;
var
  I: Integer;
  C: Char;
begin
  { Abschließende Pfadtrenner entfernen, wie auf der App-Seite. }
  while (Length(Path) > 0) and ((Path[Length(Path)] = '\') or (Path[Length(Path)] = '/')) do
    SetLength(Path, Length(Path) - 1);

  Result := '';
  for I := 1 to Length(Path) do
  begin
    C := Path[I];
    if ((C >= 'a') and (C <= 'z')) or ((C >= '0') and (C <= '9')) then
      Result := Result + C
    else if (C >= 'A') and (C <= 'Z') then
      Result := Result + Chr(Ord(C) + 32)
    else
      Result := Result + C;
  end;

  { GetSHA256OfUnicodeString has the same UTF-16LE input representation as
    Encoding.Unicode + SHA256 on the .NET side. The fixed-length result also
    avoids collisions caused by replacing all punctuation with underscores. }
  Result := GetSHA256OfUnicodeString(Result);
end;

function GetInstanceKey(): String;
begin
  Result := MakeInstanceKey(ExpandConstant('{app}') + '\app');
end;

function IsInstalledInstanceRunning(): Boolean;
begin
  Result := CheckForMutexes('Local\YTMPresence.Running.' + GetInstanceKey());
end;

procedure RequestInstalledInstanceShutdown();
var
  EventHandle: THandle;
begin
  EventHandle := OpenEventW(EVENT_MODIFY_STATE, False, 'Local\YTMPresence.Shutdown.' + GetInstanceKey());
  if EventHandle <> 0 then
  begin
    SetEvent(EventHandle);
    CloseHandle(EventHandle);
  end;
end;

procedure StopInstalledInstance();
var
  WaitedMs: Integer;
begin
  if not IsInstalledInstanceRunning() then
    exit;

  RequestInstalledInstanceShutdown();

  WaitedMs := 0;
  while IsInstalledInstanceRunning() and (WaitedMs < ShutdownWaitTotalMs) do
  begin
    Sleep(ShutdownPollMs);
    WaitedMs := WaitedMs + ShutdownPollMs;
  end;

  if not IsInstalledInstanceRunning() then
    // Kurze Karenz, bis der Prozess seine Datei-Handles vollständig freigegeben hat.
    Sleep(500);
  // Reagiert die Instanz nicht, übernimmt CloseApplications=force (Restart
  // Manager) und schließt nur Prozesse mit offenen Dateien im Zielverzeichnis.
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  StopInstalledInstance();
  Result := '';
end;

function InitializeUninstall(): Boolean;
begin
  StopInstalledInstance();
  Result := True;
end;
