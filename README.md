# YTMPresence

YTMPresence zeigt den aktuellen YouTube-Music-Track als Discord Rich Presence an. Das Projekt besteht aus zwei Teilen:

- einer lokalen Companion-App im Tray, die mit Discord spricht
- einer Browser-Extension, die den aktuellen YouTube-Music-Status an den Companion sendet

**Die Extension gibt es im Chrome Web Store:**
[YTM Discord Presence Bridge](https://chromewebstore.google.com/detail/ytm-discord-presence-brid/obdinnbalnbmpkcaopnhnkdgdkcgligi)

Sie funktioniert nur zusammen mit der Companion-App für Windows aus den [Releases](https://github.com/Kentarohakase/YTMPresence/releases) — Discord Rich Presence lässt sich aus dem Browser heraus nicht setzen.

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

### 3. Extension installieren

In Chrome oder Edge über den Store — das ist der empfohlene Weg, weil Updates dann automatisch kommen:

[YTM Discord Presence Bridge im Chrome Web Store](https://chromewebstore.google.com/detail/ytm-discord-presence-brid/obdinnbalnbmpkcaopnhnkdgdkcgligi)

Alternativ als entpackte Erweiterung, etwa zum Entwickeln oder Testen:

1. `chrome://extensions` oder `edge://extensions` öffnen
2. Entwicklermodus aktivieren
3. `Entpackte Erweiterung laden` auswählen
4. den Ordner `extension` auswählen

Beides gleichzeitig zu installieren ist nicht sinnvoll: Beide Fassungen verbinden sich mit demselben Companion.

In Firefox Desktop (ab 126):

1. `about:debugging#/runtime/this-firefox` öffnen
2. `Temporäres Add-on laden…` auswählen
3. die `manifest.json` aus der Firefox-Variante der Extension auswählen

Die Oberfläche von Extension und Tray-App folgt der Browser- bzw. Windows-Sprache (Deutsch und Englisch werden unterstützt).

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

Die Tray-App kann über `Nach Updates suchen` im Tray-Menü das neueste GitHub Release prüfen. Wenn ein Setup-EXE im Release vorhanden ist, kann die App das Update direkt herunterladen, verifizieren und das Setup starten. Beim Start wird automatisch einmal geprüft, sofern `CheckForUpdatesOnStartup` in den Einstellungen aktiv ist.

Der Update-Download ist abgesichert:

- **Verpflichtende SHA256-Prüfung**: Ein Setup wird nur gestartet, wenn das Release eine `SHA256SUMS.txt` enthält, darin genau eine exakt passende Zeile für die Setup-Datei steht und der berechnete Hash exakt übereinstimmt. Fehlt die Prüfsummendatei oder die Zeile, ist der Hash ungültig oder widersprüchlich, wird das Update nicht ausgeführt.
- **Erlaubte Updatequellen**: Reale Updates werden ausschließlich per HTTPS von `api.github.com`, `github.com`, `release-assets.githubusercontent.com` und dem historischen Asset-Host `objects.githubusercontent.com` geladen. Ausgangs-URL, jeder Redirect und das finale Ziel werden einzeln geprüft; HTTP, direkte IP-Adressen, Loopback-, private und link-local Ziele sowie fremde Hosts werden abgelehnt. Normale TLS-Zertifikatsprüfung bleibt aktiv; eine eigene DNS-Pinning-Bibliothek wird bewusst nicht implementiert. Nur für den lokalen QA-Test (`scripts/test-update-flow.ps1`) kann per explizitem Settings-Flag `AllowLoopbackUpdateSource` eine HTTP-Loopback-Quelle erlaubt werden.
- **Größenlimits und Timeouts**: Setup (300 MB), Prüfsummendatei (64 KB) und Release-Metadaten (4 MB) haben harte Obergrenzen, die vor dem Download (Content-Length) und während des Streamings erzwungen werden. Verbindungsaufbau, Datenstillstand und Gesamtdauer sind mit Timeouts abgesichert; Abbrüche über das Fenster oder beim App-Beenden räumen sauber auf.
- **Temporäre Dateien**: Jeder Updateversuch nutzt ein eigenes eindeutiges Verzeichnis unter `%TEMP%\YTMPresence\updates`. Der Download läuft in eine `.part`-Datei und wird erst nach erfolgreicher Hash-Prüfung auf den finalen Namen verschoben; eine `.part`-Datei wird nie gestartet. Fehler, Benutzerabbruch, Fensterschließen und App-Shutdown brechen laufende I/O ab und räumen bestmöglich auf. Kann ein bereits gestartetes Setup seine Datei noch sperren, wird das verwaiste Verzeichnis beim nächsten App-Start altersbasiert bereinigt.
- **Prüfung direkt vor dem Start**: Unmittelbar vor dem Prozessstart werden Existenz, Dateiname, Größe und SHA256 erneut geprüft; die Datei bleibt während des Starts gegen Veränderung gesperrt. Die App beendet sich erst, wenn das Setup tatsächlich gestartet wurde — schlägt der Start fehl, bleibt YTMPresence benutzbar.

**Grenzen der SHA256-Verifikation**: Die `SHA256SUMS.txt` stammt aus demselben GitHub Release wie das Setup. Sie schützt vor Übertragungsfehlern und versehentlicher Manipulation einzelner Dateien, stellt aber keine kryptografische Authentizität des gesamten Releases sicher: Wer Setup **und** Prüfsummendatei im Release ersetzen kann (z. B. mit einem kompromittierten Release-Konto), wird dadurch nicht erkannt. Sie ist kein Ersatz für eine digitale Signatur oder einen Herausgebernachweis.

**Beenden der laufenden App bei Installation**: Der Installer beendet keine Prozesse anhand des Dateinamens. Er bittet zuerst die aus dem Zielverzeichnis laufende Instanz über einen pfadgebundenen lokalen Mechanismus, sich regulär zu beenden. Reagiert sie nicht, schließt der Windows Restart Manager nur Prozesse, die tatsächlich Dateien im Installationsordner geöffnet halten. Eine parallel laufende portable Kopie in einem anderen Ordner bleibt unberührt.

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

Voraussetzung: .NET 10 SDK und Inno Setup 6 (`ISCC.exe`) für das Setup-EXE. GitHub Actions installiert Inno Setup automatisch.

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
artifacts\release\YTMPresence-extension-firefox.zip
artifacts\release\YTMPresence-<version>-win-x64.zip
artifacts\release\YTMPresence-<version>-win-x64-setup.exe
artifacts\release\extension\
artifacts\release\SHA256SUMS.txt
```

`YTMPresence-extension.zip` ist die Chromium-Variante (Chrome/Edge), `YTMPresence-extension-firefox.zip` enthält dasselbe Paket mit `browser_specific_settings` für Firefox (ab Version 126).

Das Setup-EXE wird in Release-Builds mit Inno Setup gebaut und bleibt beim Dateinamen `YTMPresence-<version>-win-x64-setup.exe`, damit der App-Update-Flow kompatibel bleibt.

Für eine normale Weitergabe ist das Setup-EXE am bequemsten. Es installiert YTMPresence ohne Adminrechte nach `%LOCALAPPDATA%\Programs\YTMPresence` und legt Startmenü-Verknüpfungen an. Das versionierte Komplett-ZIP enthält weiterhin die App, die Extension und eine `RELEASE.txt`.
Das Release-Skript prüft das Paket nach dem Build automatisch. Alte versionierte App-/ZIP-Artefakte für denselben Runtime-Zieltyp werden standardmäßig entfernt. Mit `-KeepOldArtifacts` bleiben sie erhalten.
Vor dem Tagging hilft die [Release-QA-Checkliste](docs/RELEASE_QA.md), Installation, Update, Deinstallation und GitHub-Release-Artefakte einmal geordnet zu prüfen.

Ein vorhandenes Release kannst du auch separat prüfen:

```powershell
.\scripts\verify-release.ps1
```

Die Verifikation schreibt nichts mehr um: `SHA256SUMS.txt` entsteht ausschließlich in `package-release.ps1`, nachdem alle Artefakte final sind. `verify-release.ps1` prüft anschließend, dass jedes Artefakt genau eine passende Hashzeile hat und keine veralteten, doppelten oder unbekannten Zeilen enthalten sind (`-RequireInstaller`/`-RequireSetup` verlangt zusätzlich das Setup-EXE).

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

### Einstellungen beschädigt

Kann `settings.json` nicht als gültige Konfiguration gelesen werden, bewahrt YTMPresence die Datei im selben Ordner als `settings.corrupt-<timestamp>.json` auf und startet mit sicheren Standardwerten sowie einem neuen Token. Danach muss der neue Token erneut in der Extension gespeichert werden.
