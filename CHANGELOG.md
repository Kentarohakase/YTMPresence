# Changelog

Alle wichtigen Änderungen an YTMPresence werden in dieser Datei dokumentiert.

## [Unreleased]

### Fixed

- **Die Discord-Presence ist nicht mehr fest deutsch.** Wiedergabestatus, Pausenzeile, Werbehinweis und die Beschriftung des Track-Buttons waren fest verdrahtet — ein englischer Nutzer zeigte seinen Kontakten damit „Pausiert bei 1:23“ und einen Button namens „Auf YouTube Music öffnen“. Das ist der Text, den **andere** lesen, nicht die eigene Oberfläche. `PresenceText` im Core-Projekt übersetzt jetzt nach Systemsprache, mit Englisch als Rückfallebene statt Deutsch.
- Diagnosefenster und Erste-Schritte-Fenster folgen ebenfalls der Systemsprache. Serverzustand, Verbindungs- und Discord-Zeilen, Alterangaben, die Update-Check-Zeile und die Fehlermeldung beim Oeffnen von YouTube Music waren fest deutsch. Damit ist die gesamte Oberflaeche uebersetzt; deutsch bleiben nur noch die Logmeldungen, die sich an dich richten.
- Der Mini-Player folgt jetzt der Windows-Sprache. Wiedergabestatus, die Meldung ohne Track und die Beschriftung des Zurueck-Kommandos waren fest auf Deutsch verdrahtet und blieben deutsch, auch wenn die uebrige Oberflaeche auf Englisch lief.

## [1.3.1] - 2026-08-09

### Changed

- **Die Extension fordert die Berechtigung `tabs` nicht mehr an.** Gebraucht wurde sie nie: `tabs.query` mit URL-Filter und `tabs.sendMessage` funktionieren über die Host-Berechtigung für `https://music.youtube.com/*`, `tabs.create` braucht gar keine Berechtigung, und aus den Treffern wird ausschließlich die Tab-ID gelesen. Chrome zeigte dafür bei der Installation „Deinen Browserverlauf lesen" an — für eine Fähigkeit, von der die Extension nie Gebrauch gemacht hat.
- Kurzbeschreibung und Store-Text nennen die Companion-App jetzt zuerst. Ohne sie ist die Extension funktionslos, weil Discord Rich Presence sich aus dem Browser heraus nicht setzen lässt. Die Kurzbeschreibung stammt aus `_locales/<sprache>/messages.json` und wird von Chrome aus dem Manifest übernommen, nicht aus dem Store-Dashboard.
- `docs/STORE_SUBMISSION_CHECKLIST.md` enthält die Angaben zur Datenerhebung für das Chrome-Dashboard, abgestimmt auf die bereits deklarierten Firefox-Metadaten.

## [1.3.0] - 2026-08-09

### Fixed

- **Die Presence lässt sich wieder entfernen.** Zum Löschen sendete der Discord-IPC-Client den Befehl `CLEAR_ACTIVITY` — den es im RPC-Protokoll nicht gibt; dokumentiert sind `SET_ACTIVITY`, `SEND_ACTIVITY_JOIN_INVITE` und `CLOSE_ACTIVITY_REQUEST`. Discord lehnte den Befehl ab, die Ablehnung wurde verworfen, und die App meldete weiterhin „Discord OK". In der Folge blieb die Presence stehen: `OnlyShowWhenPlaying` wirkte nicht, `PausedClearSeconds` wirkte nicht, und auch beim Beenden verschwand sie nicht. Gelöscht wird jetzt über `SET_ACTIVITY` mit `activity: null`.
- **Track-URLs verlieren ihre Prozent-Kodierung nicht mehr.** Zur Normalisierung wurde an drei Stellen `Uri.ToString()` benutzt — laut Dokumentation die *unescaped* kanonische Form („All characters are unescaped except #, ? and %"). Aus `?q=Sam%20Smith` wurde dadurch `?q=Sam Smith` mit echten Leerzeichen. Discord lehnte die Aktivität daraufhin komplett ab (`"url" must be a valid uri`), und es erschien überhaupt keine Presence. Betroffen waren die Suchlinks, auf die zurückgefallen wird, wenn keine Video-ID zu finden ist. Es wird jetzt `AbsoluteUri` verwendet.
- Eine ungültige Button-URL kostet nicht mehr die gesamte Presence. Die URL wird vor dem Bauen des Buttons geprüft; hält sie nicht stand, entfällt nur dieser Button und eine Logzeile nennt den Grund.
- Vor einem Presence-Update wird nicht mehr erst gelöscht. Der frühere „Discord Cache Fix" sendete bei jedem Track- und Play/Pause-Wechsel ein Löschen und wartete 200 ms. Das blieb folgenlos, solange das Löschen den nicht existierenden Befehl `CLEAR_ACTIVITY` benutzte — mit dem reparierten Löschen hätte derselbe Ablauf die Presence bei jedem Songwechsel kurz verschwinden lassen. `SET_ACTIVITY` ersetzt die Aktivität ohnehin von sich aus.
- Fehlerantworten von Discord werden protokolliert statt verworfen. Genau diese Lücke hat den falschen Befehl über die gesamte Projektlaufzeit verdeckt.

### Added

- Das Diagnosefenster zeigt den letzten erfolgreichen Update-Check. Fehler beim automatischen Start-Check landen bewusst nur im Log, damit nicht bei jedem Hochfahren ohne Netz eine Sprechblase erscheint — dadurch konnte er aber monatelang scheitern, ohne dass es jemand bemerkt. Liegt der letzte Erfolg über zwei Wochen zurück, weist die Zeile ausdrücklich darauf hin; ist die Prüfung beim Start abgeschaltet, steht auch das dort. Der Zeitpunkt wird in den Einstellungen gespeichert und ist im Diagnosebericht enthalten.

## [1.2.0] - 2026-08-05

### Added

- `scripts/verify-extension-archives.ps1` prüft die gebauten Extension-Archive so, wie eine Store-Uploadverarbeitung sie sieht: Eintragsnamen (Pfadtrenner, absolute Pfade, Traversal), echtes Entpacken auf die Platte und Auflösung aller `__MSG_*__`-Platzhalter gegen den Katalog der Standardsprache.
- CI baut die Release-Artefakte jetzt selbst (`package-release.ps1 -SkipInstaller`), verifiziert sie und lintet die **entpackte** Firefox-Variante aus dem gebauten Archiv statt des Quellordners. Ein Lint gegen die Quelle übersieht Verpackungsfehler, weil er die Archive nie anfasst.

### Changed

- **Der Companion-WebSocket prüft jetzt den `Origin` des Handshakes.** WebSocket-Verbindungen unterliegen nicht der Same-Origin-Policy: Bisher konnte jede besuchte Webseite den lokalen Companion ansprechen und über `YTM_TEST` unbegrenzt Token raten. Praktisch war das durch das 256-Bit-Token abgesichert, blieb aber als Fingerprinting-Fläche und offener Endpunkt bestehen. Zugelassen sind jetzt Extension-Origins (`chrome-extension:`, `moz-extension:`, `safari-web-extension:`) und fehlender Origin für lokale Nicht-Browser-Clients; Web-Origins werden mit HTTP 403 abgewiesen.
- Sockets, die innerhalb von 60 Sekunden kein gültiges Token vorzeigen, werden geschlossen. Ein gültiges Token im Verbindungstest zählt dabei als Nachweis, weil die Extension die Verbindung bei pausierter Wiedergabe genau darüber offen hält.
- Die doppelte Implementierung der Instanzschlüssel-Berechnung (`InstallerCoordination.GetInstanceKey` und `MakeInstanceKey` in `installer/YTMPresence.iss`) ist auf beiden Seiten als solche gekennzeichnet, mit gemeinsamem Testvektor. `docs/RELEASE_QA.md` prüft die Selbstbeendigung jetzt als eigenen Punkt: Sie ist der einzige sichtbare Nachweis, dass beide Seiten denselben Schlüssel berechnen — driften sie, fällt der Installer still auf den Restart Manager zurück und das Update wirkt trotzdem erfolgreich.
- Das Setup-Asset eines Releases wird genau einmal ausgewählt; Name und Download-URL stammen anschließend aus demselben Asset. Mehrere Assets mit `-setup.exe` werden wie mehrdeutige Prüfsummenzeilen abgelehnt, statt still das erste zu nehmen – die Reihenfolge der GitHub-Antwort ist nicht zugesichert.
- Releases mit `prerelease: true` werden nicht mehr als Update angeboten. `/releases/latest` filtert sie zwar bereits, aber `UpdateApiUrl` ist konfigurierbar.
- Auswahl und Verifikation des Prüfsummen-Assets vergleichen den Dateinamen jetzt beide ordinal. Ein abweichend geschriebenes `sha256sums.txt` wird dadurch gar nicht erst angeboten, statt später mit einer irreführenden Sicherheitsmeldung abzubrechen. Die Artefaktnamen liegen dafür in `UpdateArtifactNames` als gemeinsame Quelle.

### Fixed

- **Pausierte Wiedergabe wird wieder als Pause erkannt.** Die Extension las den Wiedergabestatus bevorzugt aus `navigator.mediaSession.playbackState` und fiel nur bei leerem Wert auf das Medienelement zurück. Da YouTube Music diesen Wert beim Pausieren nicht zuverlässig aktualisiert, meldete die Extension dauerhaft „läuft" — die Presence blieb in Discord stehen, und weder `OnlyShowWhenPlaying` noch `PausedClearSeconds` konnten greifen, weil der Companion nie eine Pause zu sehen bekam. Maßgeblich ist jetzt `paused` des Medienelements; `mediaSession` dient nur noch als Rückfall. Sprünge in der Zeitleiste (`seeking`) werden übersprungen, damit die Presence beim Spulen nicht flackert.
- Die Suche nach dem Medienelement berücksichtigt jetzt auch `<video>`. Fand YouTube Music kein `<audio>`, hatten Position, Dauer und Wiedergabestatus zuvor gar keine Quelle, und der Status fiel stillschweigend auf „läuft" zurück.
- **Die Presence verlinkt nicht mehr ein beliebiges Video von der Seite.** `getPlayerBarWatchUrl` enthielt Selektoren ohne Container-Bezug (`a[href*="/watch?v="]` und `a[href*="music.youtube.com/watch?v="]`), die den ersten passenden Link im gesamten Dokument greifen — auf YouTube Music also Einträge aus der Warteschlange, Empfehlungskacheln oder der Seitenleiste. Da diese Quelle zusätzlich Vorrang hatte, wurde ein nie abgespielter Track verlinkt. Es zählen jetzt ausschließlich Anker innerhalb von `<ytmusic-player-bar>`, und das Artwork steht an erster Stelle: Es wird vom Player pro Track gesetzt und meint als einzige Quelle zuverlässig den laufenden Titel.
- Die Track-URL berücksichtigt jetzt auch die Adresszeile. YouTube Music liefert (Stand 2026-08) weder die Player-Leisten-Anker noch `canonical`, und `og:url` zeigt auf `/app_shell` ohne Video-ID — die Ermittlung hing damit allein am Artwork als letzter Rückfallebene. Die Kandidaten sind neu nach Verlässlichkeit geordnet: Player-Leiste, Artwork, Adresszeile, canonical. Die zuvor identischen Zweige für `video`- und Standardmodus wurden zusammengeführt.
- Release-ZIPs verwenden jetzt Forward Slashes als Pfadtrenner. `Compress-Archive` schreibt unter Windows PowerShell 5.1 Backslashes, was gegen die ZIP-Spezifikation verstößt: Spezifikationstreue Entpacker – darunter die Uploadverarbeitung von Chrome Web Store und addons.mozilla.org – lesen `_locales\en\messages.json` als flachen Dateinamen statt als Verzeichnis, wodurch die `__MSG_*__`-Platzhalter im Manifest nicht auflösen. Betroffen waren beide Extension-ZIPs und das Bundle-ZIP.
- `verify-release.ps1` normalisiert Backslash-Einträge nicht mehr auf Forward Slashes, sondern bricht mit Nennung der betroffenen Einträge ab. Die bisherige Normalisierung hat genau den Defekt als in Ordnung gemeldet, den die Prüfung finden sollte.

## [1.1.0] - 2026-08-05

### Added

- Versionskonsistenzprüfung für App, Assembly-Metadaten, Extension und Release-Changelog; CI und Release-Paketierung brechen bei Abweichungen ab.
- Settings-Recovery sichert beschädigte JSON-Dateien als `settings.corrupt-<timestamp>.json` und erzeugt anschließend gültige Defaults.
- Update-Sicherheitskomponenten in `YTMPresence.Core.Updates` (URL-Validierung, Prüfsummen-Parser, Größenlimits, temporäre Arbeitsverzeichnisse, Download-Service, Start-Verifikation) mit umfangreichen Tests für Prüfsummen-, Download-, Redirect- und Prozessstart-Fälle.
- Settings-Flag `AllowLoopbackUpdateSource` (Standard: aus) als explizite Testkonfiguration für den lokalen QA-Update-Server; `scripts/test-update-flow.ps1` setzt es automatisch und stellt die Originaleinstellungen wieder her.

### Changed

- Einstellungen werden über eine temporäre Datei und atomaren Dateiersatz gespeichert.
- Firefox-Pakete deklarieren die lokale Übertragung von Browsing-Aktivität und Website-Inhalten über Mozillas Consent-Metadaten, ohne die bestehende Mindestversion 126 anzuheben.
- **Updates werden nur noch nach erfolgreicher SHA256-Verifikation ausgeführt.** Fehlende `SHA256SUMS.txt`, fehlende, doppelte oder widersprüchliche Setup-Zeilen, ungültige oder abweichende Hashes blockieren die Installation; der bisherige Start „ohne Prüfsummenvergleich“ entfällt.
- Update-Quellen sind auf HTTPS zu `api.github.com`, `github.com`, `release-assets.githubusercontent.com` und `objects.githubusercontent.com` beschränkt; Metadaten- und Asset-Redirects werden einzeln validiert (max. 5, Schleifenerkennung), Downloads haben Größenlimits (Setup 300 MB, Prüfsummen 64 KB, Metadaten 4 MB) sowie Verbindungs-, Stall- und Gesamt-Timeouts. Eigene `UpdateApiUrl`-Werte außerhalb dieser Richtlinie werden mit einer klaren Fehlermeldung abgelehnt.
- Jeder Updateversuch nutzt ein eindeutiges temporäres Verzeichnis mit `.part`-Datei und atomarem Umbenennen nach der Prüfung; Fehler und Abbrüche räumen auf, verwaiste Verzeichnisse werden beim App-Start altersbasiert bereinigt.
- Unmittelbar vor dem Setup-Start werden Existenz, Name, Größe und SHA256 erneut geprüft und die Datei während des Starts gegen Veränderung gesperrt; YTMPresence beendet sich erst nach tatsächlich gestartetem Setup und bleibt bei einem Fehlstart benutzbar.
- Der Inno-Setup-Installer beendet keine Prozesse mehr per `taskkill` nach Dateinamen: Er bittet die installierte Instanz über ein mit SHA256 kollisionsresistent an den Zielpfad gebundenes Shutdown-Event, sich regulär zu beenden, und nutzt sonst den Windows Restart Manager (`CloseApplications=force`), der nur Prozesse mit offenen Dateien im Installationsordner schließt. Portable Kopien und fremde gleichnamige Prozesse bleiben unberührt.
- `verify-release.ps1` verifiziert `SHA256SUMS.txt` nur noch (Existenz, exakt eine gültige Zeile pro Artefakt, korrekte Hashes, keine veralteten/doppelten/unbekannten Zeilen) statt sie neu zu schreiben; erzeugt wird die Liste ausschließlich in `package-release.ps1`, nachdem alle Artefakte final sind. Der Release-Workflow baut und testet die Solution vor dem Paketieren.

### Fixed

- WebSocket-Textnachrichten bleiben korrekt, wenn ein UTF-8-Zeichen über mehrere Frames fragmentiert ist; Binär- und übergroße Nachrichten werden weiterhin verworfen.
- Serverstart, -stopp und Presence-Updates sind serialisiert; Idle-Tasks und Discord-Ressourcen werden bei Neustarts vollständig beendet.
- Ein rückwärtskompatibles Keepalive über den bestehenden Verbindungstest hält die Manifest-V3-WebSocket-Verbindung bei pausierter Wiedergabe aktiv, ohne Pause- oder Idle-Timeouts zurückzusetzen.
- Die finale Release-Prüfsummenliste enthält jetzt auch Firefox-ZIP und – sofern gebaut – das Setup-EXE.

## [1.0.0] - 2026-06-20

### Added

- Testprojekt `YTMPresence.Core.Tests` (xUnit) mit Tests für Security-Token, Settings-Normalisierung, Update-Check (SemVer/Asset-Auswahl), Presence-Logik und CompanionServer-Token-Authentifizierung.
- PR-CI-Workflow (`.github/workflows/ci.yml`): Build, Tests und `web-ext lint` bei Pull Requests und Pushes auf `main`.
- Englische Lokalisierung der Browser-Extension über `chrome.i18n` (`_locales/en` als Standard, `_locales/de`); Popup, Optionen und Manifest sind jetzt DE/EN.
- Firefox-Unterstützung: Release-Pipeline erzeugt zusätzlich `YTMPresence-extension-firefox.zip` mit `browser_specific_settings`.

### Changed

- Tray-Fenster (Erste Schritte, Einstellungen, Mini-Player, Update) verwenden jetzt durchgängig die zentralen `UiText`-Strings und sind damit DE/EN.
- Discord-IPC-Client probiert bei nicht laufendem Discord nicht mehr bei jeder Nachricht alle Pipes durch, sondern legt eine kurze Cooldown-Phase ein und verbindet sich automatisch neu.
- Release-Verifikation prüft die neuen Extension-Dateien (`i18n.js`, `_locales`) und beide Extension-ZIPs (Chromium + Firefox).

### Removed

- Totes Legacy-Projekt `YTMPresence.Tray` (nicht Teil der Solution) entfernt.

## [0.8.11] - 2026-06-01

### Fixed

- Inno-Setup-Update beendet nur noch YTMPresence selbst und nicht mehr das daraus gestartete Setup.

## [0.8.10] - 2026-05-15

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
