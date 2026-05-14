# Chrome Web Store Vorbereitung

Dieses Dokument enthält vorbereitete Texte für eine spätere Veröffentlichung der YTMPresence Extension im Chrome Web Store oder in kompatiblen Browser-Stores.

## Store-Name

YTM Discord Presence Bridge

## Kurzbeschreibung

Zeigt YouTube Music als Discord Rich Presence über einen lokalen Companion.

## Beschreibung

YTMPresence verbindet YouTube Music im Browser mit einer lokal laufenden Companion-App für Windows. Die Extension liest den aktuellen Playback-Status auf YouTube Music aus und sendet ihn sicher über einen lokalen WebSocket an den Companion. Der Companion aktualisiert daraus Discord Rich Presence und kann den aktuellen Track im Mini-Player anzeigen.

Funktionen:

- Discord Rich Presence für YouTube Music
- Lokale Verbindung über Security Token
- Popup mit Companion-, Token- und Track-Status
- Optionsseite mit Verbindungstest
- Mini-Player-Steuerung über den lokalen Companion
- Keine externen Analyse-, Tracking- oder Werbedienste

## Privacy-Hinweis für den Store

Die Extension verarbeitet Playback-Daten von YouTube Music, um die Nutzerfunktion "Discord Rich Presence für YouTube Music" bereitzustellen. Daten werden nur an den lokal installierten YTMPresence Companion auf `localhost` beziehungsweise `127.0.0.1` gesendet. Die Extension überträgt keine Daten an externe Server des Entwicklers und nutzt keine Tracking- oder Werbedienste.

Vollständiger Privacy-Hinweis:

https://github.com/Kentarohakase/YTMPresence/blob/main/docs/EXTENSION_PRIVACY.md

## Berechtigungsbegründung

`storage`
: Speichert Security Token und lokale Companion-URL im Browserprofil.

`tabs`
: Findet offene YouTube-Music-Tabs, um Verbindungstests und Player-Kommandos zuverlässig an den richtigen Tab weiterzuleiten.

`https://music.youtube.com/*`
: Erlaubt der Extension, Playback-Daten ausschließlich auf YouTube Music auszulesen.

`http://127.0.0.1/*` und `http://localhost/*`
: Erlaubt die Verbindung zur lokal laufenden Companion-App.

## Veröffentlichungsschritte

1. Release-ZIP bauen: `.\scripts\package-release.ps1`
2. `artifacts\release\YTMPresence-extension.zip` im Store hochladen.
3. Store-Name, Kurzbeschreibung und Beschreibung aus diesem Dokument übernehmen.
4. Privacy-Hinweis als URL im Store-Dashboard hinterlegen.
5. Screenshots aus Popup, Optionsseite, Mini-Player und Discord Presence hinzufügen.
6. Prüfen, ob Version in `YTMPresence/extension/manifest.json` zur Release-Version passt.

## Noch offen vor Store-Upload

- Store-Screenshots erstellen.
- Optional ein eigenes Promo-Bild für die Store-Seite erstellen.
- Privacy-Text vor öffentlicher Veröffentlichung final prüfen.
