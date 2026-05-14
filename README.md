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

1. `chrome://extensions` oder `edge://extensions` oeffnen
2. Entwicklermodus aktivieren
3. `Entpackte Erweiterung laden` auswaehlen
4. den Ordner `extension` auswaehlen

### 4. Extension konfigurieren

Oeffne die Optionen der Extension und trage ein:

```text
Security Token: Token aus dem Tray
Companion WebSocket URL: ws://127.0.0.1:17373/ws
```

Danach YouTube Music neu laden:

```text
https://music.youtube.com/
```

Mit `Verbindung testen` in den Extension-Optionen kannst du pruefen, ob Companion URL und Token zusammenpassen.

Das Extension-Icon zeigt den aktuellen Verbindungsstatus als Badge an. Im Popup siehst du Companion-Status, Token-Status und den zuletzt empfangenen Track.

## Changelog

Alle Release-Aenderungen stehen in [CHANGELOG.md](CHANGELOG.md).

## Tray-Menue

- `Einstellungen...`: oeffnet das Settings-Fenster fuer Token, Server, Presence und Discord Client ID
- `Mini-Player oeffnen`: zeigt aktuellen Track, Cover, Fortschritt, Play/Pause, Zurueck/Weiter und einen Button zum Oeffnen des Tracks
- `Diagnose oeffnen`: zeigt Server-, Extension-, Discord-, Track- und Security-Status
- `Presence nur wenn Musik laeuft`: loescht die Discord-Presence sofort beim Pausieren
- `Werbung ignorieren`: sendet bei Werbung kein Discord-Update
- `Autostart aktivieren`: startet YTMPresence beim Windows-Login
- `Token kopieren`: kopiert den aktuellen Security Token
- `Neuen Token generieren`: erzeugt einen neuen Token und startet den Companion neu
- `Log oeffnen`: oeffnet das aktuelle Logfile

## Release bauen

Voraussetzung: .NET 10 SDK.

```powershell
.\scripts\package-release.ps1
```

Der Standard-Build ist framework-dependent. Auf einem anderen Rechner muss also die .NET 10 Desktop Runtime installiert sein.

Fuer ein groesseres Paket, das die Runtime mitbringt:

```powershell
.\scripts\package-release.ps1 -SelfContained
```

Das Skript erstellt:

```text
artifacts\release\YTMPresence-<version>-win-x64-app\
artifacts\release\YTMPresence-extension.zip
artifacts\release\YTMPresence-<version>-win-x64.zip
artifacts\release\extension\
artifacts\release\SHA256SUMS.txt
```

Fuer eine normale Weitergabe ist das versionierte Komplett-ZIP am bequemsten. Es enthaelt die App, die Extension und eine `RELEASE.txt`.
Das Release-Skript prueft das Paket nach dem Build automatisch. Alte versionierte App-/ZIP-Artefakte fuer denselben Runtime-Zieltyp werden standardmaessig entfernt. Mit `-KeepOldArtifacts` bleiben sie erhalten.

Ein vorhandenes Release kannst du auch separat pruefen:

```powershell
.\scripts\verify-release.ps1
```

## Fehlerbehebung

### Discord zeigt nichts an

- Discord Desktop-App muss laufen.
- Companion muss im Tray sichtbar sein.
- YouTube Music Tab einmal neu laden.
- Im Tray `Diagnose oeffnen` pruefen.
- Im Tray `Log oeffnen` pruefen.

### Extension ist nicht verbunden

- Token in der Extension pruefen.
- Companion URL muss normalerweise `ws://127.0.0.1:17373/ws` sein.
- In den Extension-Optionen `Verbindung testen` verwenden.
- Nach Token-Aenderungen den YouTube-Music-Tab neu laden.

### Token ungueltig

Wenn im Tray oder Diagnosefenster ungueltige Tokens gezaehlt werden:

1. `Token kopieren`
2. Extension-Optionen oeffnen
3. Token ersetzen
4. speichern
5. YouTube Music neu laden

### Port belegt

Der Companion nutzt standardmaessig Port `17373`. Falls der Port belegt ist, kann er im Settings-Fenster oder in `%APPDATA%\YTMPresence\settings.json` geaendert werden. Danach muss auch die Companion WebSocket URL in der Extension angepasst werden.
