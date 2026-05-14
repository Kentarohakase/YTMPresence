# Changelog

Alle wichtigen Aenderungen an YTMPresence werden in dieser Datei dokumentiert.

## [Unreleased]

- Noch keine Aenderungen.

## [0.8.0] - 2026-05-15

### Added

- Mini-Player-Controls fuer Play/Pause, Zurueck und Weiter.
- Rueckkanal vom Companion zur Extension fuer Player-Commands.
- Neue Extension-Optionsseite mit Live-Status, Verbindungstest, Standard-URL und YT-Music-Button.
- Release-Verifikation per `scripts/verify-release.ps1`.
- Automatische SHA256-Pruefsummen in `artifacts/release/SHA256SUMS.txt`.
- Automatisches Aufraeumen alter versionierter Release-Artefakte im Package-Skript.
- GitHub-Lizenzdatei und zusammengefuehrte `.gitignore`.

### Changed

- Extension-Version und App-Version auf `0.8.0` angeglichen.
- Mini-Player-Standardbreite an die neuen Controls angepasst.
- Release-Paketierung prueft nach dem Build, ob App, Extension und ZIP-Inhalte vollstaendig sind.

### Fixed

- Release-Bundle kopiert App-Dateien jetzt korrekt in den `app`-Ordner.
- Verhindert unvollstaendige Releases durch automatische ZIP-Strukturpruefung.

## [0.7.0] - 2026-05-14

### Added

- Extension-Popup mit Companion-Status, Token-Status, URL und letztem Track.
- Badge am Extension-Icon fuer Verbindungsstatus.
- Verbindungstest direkt im Popup.
- Schnellere State-Updates bei Play/Pause, Seek und YouTube-Music-Navigation.

### Changed

- Robustere Injection-Guards fuer Content- und Page-Script.
- Extension-README-Hinweise zum Popup und Badge ergaenzt.

## Vor 0.7.0

### Added

- Lokale WPF-Tray-App als Discord Rich Presence Companion.
- Browser-Extension fuer YouTube Music Playback-Daten.
- Security Token zwischen Extension und Companion.
- Einstellungen fuer Token, Server, Presence und Discord Client ID.
- Mini-Player fuer Track, Cover, Fortschritt und Track-Link.
- Diagnosefenster fuer Server-, Extension-, Discord-, Track- und Security-Status.
- Versioniertes Release-Bundle mit App und Extension.

### Fixed

- Stabilere YouTube-Music-Erkennung fuer Track- und Video-Links.
- Queueing von Extension-States waehrend WebSocket-Reconnects.
- Korrekte Speicherung und Normalisierung der Mini-Player-Fenstereinstellungen.
