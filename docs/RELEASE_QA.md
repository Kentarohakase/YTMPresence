# Release-QA

Diese Checkliste ist für jeden Release-Tag gedacht, bevor das Release breit geteilt wird.

## Vor dem Tag

- `Directory.Build.props` und `YTMPresence/extension/manifest.json` haben dieselbe Version.
- `CHANGELOG.md` enthält einen Eintrag für diese Version.
- `dotnet build YTMPresence.slnx --configuration Release` läuft ohne Fehler.
- `dotnet test YTMPresence.Core.Tests/YTMPresence.Core.Tests.csproj` ist grün.
- `.\scripts\package-release.ps1` erzeugt App, Bundle-ZIP, Extension-ZIP (Chromium), Extension-ZIP (Firefox), Inno-Setup-EXE und danach `SHA256SUMS.txt` (erst nachdem alle Artefakte final sind).
- `.\scripts\verify-release.ps1 -RequireSetup` meldet die erwartete Version und den Runtime-Zieltyp und verifiziert `SHA256SUMS.txt` gegen die Artefakte, ohne die Liste neu zu schreiben.

## Release-Artefakte

- `YTMPresence-<version>-win-x64-setup.exe` startet ohne Adminrechte.
- Setup zeigt den Inno-Setup-Wizard und installiert nach `%LOCALAPPDATA%\Programs\YTMPresence`.
- `YTMPresence-<version>-win-x64.zip` enthält `app/`, `extension/` und `RELEASE.txt`.
- `YTMPresence-extension.zip` enthält Manifest, Scripts, `i18n.js`, `_locales/`, Optionen, Popup und alle Icons.
- `YTMPresence-extension-firefox.zip` enthält dieselben Dateien plus `browser_specific_settings` im Manifest.
- `SHA256SUMS.txt` enthält Bundle-ZIP, beide Extension-ZIPs und Setup-EXE.

## Installations-Test

- Setup über eine vorhandene Installation ausführen.
- YTMPresence startet nach der Installation.
- Startmenü-Verknüpfung startet die App.
- Windows zeigt YTMPresence unter den installierten Apps.
- Deinstallation entfernt Startmenü-Eintrag, Autostart-Wert, App-Ordner und Windows-Deinstallationseintrag.

## Update-Test

- Eine ältere Version installieren.
- `Nach Updates suchen` im Tray-Menü ausführen.
- Update-Menüeintrag öffnet das Update-Installer-Fenster.
- `Nur herunterladen` zeigt Fortschritt, Zielpfad und eine bestätigte SHA256-Prüfung, ohne die App zu beenden.
- `Installieren` startet das Setup erst nach erfolgreicher SHA256-Prüfung; **danach beendet sich YTMPresence selbst** (der Installer beendet die App nicht mehr per Prozessname).
- Setup-Fenster bleibt sichtbar, nachdem YTMPresence beendet wurde.
- Abbruch während des Downloads entfernt die temporären Dateien unter `%TEMP%\YTMPresence\updates`.
- Schließen des Update-Fensters und App-Shutdown brechen laufende Downloads ab und entfernen das jeweilige Arbeitsverzeichnis bestmöglich.
- Eine manipulierte oder fehlende `SHA256SUMS.txt` blockiert die Installation mit einer Fehlermeldung; die App bleibt benutzbar.
- Läuft parallel eine portable YTMPresence-Kopie aus einem anderen Ordner in einer anderen Benutzersitzung, bleibt sie von der Installation unberührt.
- Nach der Installation meldet die App die neue Version.

## Lokaler Updater-Hotfix-Test

- Release-Kandidat mit `.\scripts\package-release.ps1` erzeugen.
- Eine ältere veröffentlichte Version installieren.
- `.\scripts\test-update-flow.ps1 -SetupPath .\artifacts\release\YTMPresence-<version>-win-x64-setup.exe` starten.
- Den angezeigten manuellen In-App-Update-Test vollständig ausführen.
- Script mit Enter beenden und prüfen, dass die ursprüngliche `settings.json` wiederhergestellt wurde.

## Silent-Installations-Test

- Nur auf einem frischen Windows-Profil oder in einer VM ausführen.
- `.\artifacts\release\YTMPresence-<version>-win-x64-setup.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-` ausführen.
- Prüfen, dass `%LOCALAPPDATA%\Programs\YTMPresence\app\YTMPresence.exe` existiert.
- `%LOCALAPPDATA%\Programs\YTMPresence\unins000.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART` ausführen.
- Prüfen, dass App-Ordner, Startmenü-Eintrag und Windows-Deinstallationseintrag entfernt wurden.

## Clean-Test

- Auf einem frischen Windows-Profil oder einer VM installieren.
- Erste-Schritte-Fenster erscheint beim ersten Start.
- Token und Companion-URL lassen sich kopieren.
- Extension verbindet sich nach Speichern von Token und URL.
- Chromium-Extension (Chrome/Edge) und Firefox-Extension (`about:debugging`) verbinden sich beide.
- Bei englischer Browser-/Windows-Sprache erscheinen Extension-Popup, Optionen und Tray-Fenster auf Englisch, bei deutscher auf Deutsch.
- App ohne laufendes Discord startet stabil; Diagnose meldet Discord als nicht erreichbar und verbindet automatisch, sobald Discord läuft.
- Diagnosefenster zeigt Server, Extension und Discord sinnvoll an.
- Diagnosebericht-ZIP lässt sich erstellen und enthält keine rohe `SecurityToken`-Zeile.

## Nach dem Push

- GitHub Actions Release-Workflow ist grün.
- GitHub Release ist nicht als Draft oder Prerelease markiert.
- Release Notes entsprechen dem Changelog-Eintrag.
- Assets sind im Release vorhanden und herunterladbar.
