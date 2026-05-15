# YTMPresence

YTMPresence zeigt den aktuellen YouTube-Music-Track als Discord Rich Presence an. Das Projekt besteht aus zwei Teilen:

- einer lokalen Companion-App im Tray, die mit Discord spricht
- einer Browser-Extension, die den aktuellen YouTube-Music-Status an den Companion sendet

## Installation

### 1. Companion starten

Starte `YTMPresence.exe`. Danach erscheint das Tray-Icon im Infobereich von Windows.

Beim ersten Start erzeugt die App automatisch einen Security Token und speichert die Einstellungen unter:

```text
%APPDATA%\YTMPresence\settings.json
```

### 2. Token kopieren

Rechtsklick auf das Tray-Icon:

```text
Token kopieren
```

### 3. Extension laden

In Chrome oder Edge:

1. `chrome://extensions` oder `edge://extensions` öffnen
2. Entwicklermodus aktivieren
3. `Entpackte Erweiterung laden` auswählen
4. den Ordner `extension` auswählen

### 4. Extension konfigurieren

Öffne die Optionen der Extension und trage ein:

```text
Security Token: Token aus dem Tray
Companion WebSocket URL: ws://127.0.0.1:17373/ws
```

Danach YouTube Music neu laden:

```text
https://music.youtube.com/
```

Mit `Verbindung testen` in den Extension-Optionen kannst du prüfen, ob Companion URL und Token zusammenpassen.

Das Extension-Icon zeigt den aktuellen Verbindungsstatus als Badge an. Im Popup siehst du Companion-Status, Token-Status und den zuletzt empfangenen Track.

## Changelog

Alle Release-Änderungen stehen in [CHANGELOG.md](CHANGELOG.md).

## Extension veröffentlichen

Vorbereitete Store- und Privacy-Texte stehen unter:

- [Chrome Web Store Vorbereitung](docs/CHROME_WEB_STORE.md)
- [Extension Privacy-Hinweis](docs/EXTENSION_PRIVACY.md)
- [Store-Submission-Checkliste](docs/STORE_SUBMISSION_CHECKLIST.md)

## Updates

Die Tray-App kann über `Nach Updates suchen` im Tray-Menü das neueste GitHub Release prüfen. Wenn ein Setup-EXE im Release vorhanden ist, kann die App das Update direkt herunterladen, die SHA256-Prüfsumme prüfen und das Setup starten. Beim Start wird automatisch einmal geprüft, sofern `CheckForUpdatesOnStartup` in den Einstellungen aktiv ist.

## Tray-Menü

- `Einstellungen...`: öffnet das Settings-Fenster für Token, Server, Presence und Discord Client ID
- `Mini-Player öffnen`: zeigt aktuellen Track, Cover, Fortschritt, Play/Pause, Zurück/Weiter und einen Button zum Öffnen des Tracks
- `Diagnose öffnen`: zeigt Server-, Extension-, Discord-, Track- und Security-Status und kann einen Diagnosebericht ohne Security Token erstellen
- `Presence nur wenn Musik läuft`: löscht die Discord-Presence sofort beim Pausieren
- `Werbung ignorieren`: sendet bei Werbung kein Discord-Update
- `Autostart aktivieren`: startet YTMPresence beim Windows-Login
- `Token kopieren`: kopiert den aktuellen Security Token
- `Neuen Token generieren`: erzeugt einen neuen Token und startet den Companion neu
- `Log öffnen`: öffnet das aktuelle Logfile

## Release bauen

Voraussetzung: .NET 10 SDK und Inno Setup 6 (`ISCC.exe`) fuer das Setup-EXE. GitHub Actions installiert Inno Setup automatisch.

```powershell
.\scripts\package-release.ps1
```

Wenn Inno Setup nicht im `PATH` liegt, kann der Compiler explizit angegeben werden:

```powershell
.\scripts\package-release.ps1 -InnoSetupCompiler "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
```

Der Standard-Build ist framework-dependent. Auf einem anderen Rechner muss also die .NET 10 Desktop Runtime installiert sein.

Für ein größeres Paket, das die Runtime mitbringt:

```powershell
.\scripts\package-release.ps1 -SelfContained
```

Das Skript erstellt:

```text
artifacts\release\YTMPresence-<version>-win-x64-app\
artifacts\release\YTMPresence-extension.zip
artifacts\release\YTMPresence-<version>-win-x64.zip
artifacts\release\YTMPresence-<version>-win-x64-setup.exe
artifacts\release\extension\
artifacts\release\SHA256SUMS.txt
```

Das Setup-EXE wird in Release-Builds mit Inno Setup gebaut und bleibt beim Dateinamen `YTMPresence-<version>-win-x64-setup.exe`, damit der App-Update-Flow kompatibel bleibt.

Für eine normale Weitergabe ist das Setup-EXE am bequemsten. Es installiert YTMPresence ohne Adminrechte nach `%LOCALAPPDATA%\Programs\YTMPresence` und legt Startmenü-Verknüpfungen an. Das versionierte Komplett-ZIP enthält weiterhin die App, die Extension und eine `RELEASE.txt`.
Das Release-Skript prüft das Paket nach dem Build automatisch. Alte versionierte App-/ZIP-Artefakte für denselben Runtime-Zieltyp werden standardmäßig entfernt. Mit `-KeepOldArtifacts` bleiben sie erhalten.
Vor dem Tagging hilft die [Release-QA-Checkliste](docs/RELEASE_QA.md), Installation, Update, Deinstallation und GitHub-Release-Artefakte einmal geordnet zu prüfen.

Ein vorhandenes Release kannst du auch separat prüfen:

```powershell
.\scripts\verify-release.ps1
```

## Fehlerbehebung

### Discord zeigt nichts an

- Discord Desktop-App muss laufen.
- Companion muss im Tray sichtbar sein.
- YouTube Music Tab einmal neu laden.
- Im Tray `Diagnose öffnen` prüfen.
- Im Tray `Log öffnen` prüfen.

### Extension ist nicht verbunden

- Token in der Extension prüfen.
- Companion URL muss normalerweise `ws://127.0.0.1:17373/ws` sein.
- In den Extension-Optionen `Verbindung testen` verwenden.
- Nach Token-Änderungen den YouTube-Music-Tab neu laden.

### Token ungültig

Wenn im Tray oder Diagnosefenster ungültige Tokens gezählt werden:

1. `Token kopieren`
2. Extension-Optionen öffnen
3. Token ersetzen
4. speichern
5. YouTube Music neu laden

### Port belegt

Der Companion nutzt standardmäßig Port `17373`. Falls der Port belegt ist, kann er im Settings-Fenster oder in `%APPDATA%\YTMPresence\settings.json` geändert werden. Danach muss auch die Companion WebSocket URL in der Extension angepasst werden.
