# Changelog

Alle wichtigen Änderungen an YTMPresence werden in dieser Datei dokumentiert.

## [Unreleased]

### Changed

- Release-Skript baut Installer nur noch über Inno Setup und ist dadurch einfacher wartbar.

### Fixed

- Release-Skript entfernt veraltete Setup-Artefakte und lässt bei `-SkipInstaller` keine alte Setup-EXE liegen.
- Update-Fenster deaktiviert Download- und Installationsbuttons, wenn ein Release kein Setup-Asset enthält.
- Companion-Server verwirft nicht-textuelle oder zu große WebSocket-Nachrichten frühzeitig.
- Einstellungen verhindern URL-artige Hosts und ungültige WebSocket-Pfade.

### Removed

- Alter IExpress-basierter Installer-Fallback wurde entfernt.

## [0.8.9] - 2026-05-15

### Added

- Inno-Setup-Installer-Pipeline mit GitHub-Actions-Integration für Release-Builds.

### Changed

- Release-Skript nutzt Inno Setup bevorzugt und fällt lokal bei fehlendem `ISCC.exe` auf den bisherigen Legacy-Installer zurück.

## [0.8.8] - 2026-05-15

### Added

- Diagnosefenster kann jetzt einen Diagnosebericht als ZIP mit Status, Umgebung, redacted Settings und Logs erstellen.

## [0.8.7] - 2026-05-15

### Fixed

- Update-Installer startet das Setup jetzt mit explizitem Arbeitsverzeichnis und beendet die App nicht mehr selbst, bevor der Installer sichtbar läuft.

## [0.8.6] - 2026-05-15

### Changed

- Update-Installer kann Setups auch nur herunterladen und prüfen, ohne die App zu beenden.

## [0.8.5] - 2026-05-15

### Added

- Update-Installer-Fenster mit Setup-Download, Fortschritt, optionaler SHA256-Prüfung und anschließendem Setup-Start.

### Changed

- Update-Hinweis verweist jetzt auf die direkte Installation über das Tray-Menü.

## [0.8.4] - 2026-05-15

### Added

- Erste-Schritte-Fenster mit Token, Companion-URL, YouTube-Music-Link und direktem Zugriff auf die Einstellungen.
- Release-QA-Checkliste für Installations-, Update-, Deinstallations- und GitHub-Release-Prüfungen.

### Changed

- Update-Menü öffnet bei verfügbaren Releases direkt das Setup-EXE, wenn GitHub ein passendes Installer-Asset liefert.
- Lokale `.lscache`-Builddateien werden ignoriert.

### Fixed

- Installer wartet jetzt, bis die Setup-EXE nach der IExpress-Erzeugung lesbar ist, bevor SHA256-Prüfsummen berechnet werden.
- Mini-Player-Tooltip verwendet wieder den korrekten Umlaut in `Track öffnen`.

## [0.8.3] - 2026-05-15

### Added

- Windows-Installer registriert YTMPresence jetzt in den Windows-App-Einstellungen mit Version, Icon, Größe und Deinstallationsbefehl.

### Changed

- Installer und Release-Skript prüfen Cleanup-Ziele genauer, bevor alte Artefakte entfernt werden.
- Installer beendet beim Update nur die installierte YTMPresence-Instanz aus dem Installationsordner.

### Fixed

- Installation bricht früher mit einer klaren Fehlermeldung ab, wenn `YTMPresence.exe` nach dem Entpacken fehlt.
- Deinstallation entfernt den Windows-Deinstallationseintrag und räumt das temporäre Cleanup-Skript selbst auf.

## [0.8.2] - 2026-05-15

### Added

- User-level Windows-Installer-Paketierung mit Setup-EXE und Startmenü-Verknüpfung.
- Update-Check über GitHub Releases direkt im Tray-Menü.
- Store-Submission-Checkliste für Chrome Web Store und kompatible Browser-Stores.

### Changed

- Release-Workflow lädt künftig auch das Setup-Artefakt hoch.
- Extension-Manifest ergänzt `short_name` und `homepage_url` für Store-Veröffentlichungen.

## [0.8.1] - 2026-05-15

### Added

- GitHub Actions Workflow für automatische Tag-Releases mit ZIP-Artefakten und SHA256-Prüfsummen.
- Extension-Icons in 16, 32, 48 und 128 Pixeln.
- Privacy-Hinweis und Chrome-Web-Store-Vorbereitung für die Extension.
- `.editorconfig` für UTF-8-Dateien.

### Changed

- App-Version und Extension-Version auf `0.8.1` angehoben.
- Deutsche README-, Changelog- und Extension-Texte verwenden echte UTF-8-Umlaute.
- Release-Hinweise werden im Paket als UTF-8 geschrieben.

## [0.8.0] - 2026-05-15

### Added

- Mini-Player-Controls für Play/Pause, Zurück und Weiter.
- Rückkanal vom Companion zur Extension für Player-Commands.
- Neue Extension-Optionsseite mit Live-Status, Verbindungstest, Standard-URL und YT-Music-Button.
- Release-Verifikation per `scripts/verify-release.ps1`.
- Automatische SHA256-Prüfsummen in `artifacts/release/SHA256SUMS.txt`.
- Automatisches Aufräumen alter versionierter Release-Artefakte im Package-Skript.
- GitHub-Lizenzdatei und zusammengeführte `.gitignore`.

### Changed

- Extension-Version und App-Version auf `0.8.0` angeglichen.
- Mini-Player-Standardbreite an die neuen Controls angepasst.
- Release-Paketierung prüft nach dem Build, ob App, Extension und ZIP-Inhalte vollständig sind.

### Fixed

- Release-Bundle kopiert App-Dateien jetzt korrekt in den `app`-Ordner.
- Verhindert unvollständige Releases durch automatische ZIP-Strukturprüfung.

## [0.7.0] - 2026-05-14

### Added

- Extension-Popup mit Companion-Status, Token-Status, URL und letztem Track.
- Badge am Extension-Icon für Verbindungsstatus.
- Verbindungstest direkt im Popup.
- Schnellere State-Updates bei Play/Pause, Seek und YouTube-Music-Navigation.

### Changed

- Robustere Injection-Guards für Content- und Page-Script.
- Extension-README-Hinweise zum Popup und Badge ergänzt.

## Vor 0.7.0

### Added

- Lokale WPF-Tray-App als Discord Rich Presence Companion.
- Browser-Extension für YouTube Music Playback-Daten.
- Security Token zwischen Extension und Companion.
- Einstellungen für Token, Server, Presence und Discord Client ID.
- Mini-Player für Track, Cover, Fortschritt und Track-Link.
- Diagnosefenster für Server-, Extension-, Discord-, Track- und Security-Status.
- Versioniertes Release-Bundle mit App und Extension.

### Fixed

- Stabilere YouTube-Music-Erkennung für Track- und Video-Links.
- Queueing von Extension-States während WebSocket-Reconnects.
- Korrekte Speicherung und Normalisierung der Mini-Player-Fenstereinstellungen.
