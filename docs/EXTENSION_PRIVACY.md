# YTMPresence Extension Privacy-Hinweis

Stand: 2026-05-15

YTMPresence ist eine Browser-Extension für YouTube Music, die den aktuellen Playback-Status an eine lokal laufende Companion-App sendet. Die Companion-App zeigt diesen Status anschließend als Discord Rich Presence an.

## Welche Daten verarbeitet werden

Die Extension liest auf `https://music.youtube.com/*` die sichtbaren Playback-Daten des aktuell laufenden Tracks aus:

- Track-Titel
- Künstler
- Album, falls verfügbar
- Cover-URL, falls verfügbar
- Wiedergabestatus, Position und Dauer
- Track- oder Video-Link
- Information, ob gerade Werbung erkannt wurde

Zusätzlich speichert die Extension lokal im Browser:

- den Security Token für den lokalen Companion
- die lokale WebSocket-URL des Companion

## Wofür die Daten verwendet werden

Die Daten werden ausschließlich verwendet, um den YouTube-Music-Status an den lokalen YTMPresence Companion auf deinem Rechner zu senden. Der Companion nutzt sie für Discord Rich Presence und für den optionalen Mini-Player.

## Datenweitergabe

Die Extension sendet die Playback-Daten nur an `localhost` beziehungsweise `127.0.0.1`, also an die lokal installierte Companion-App. Die Extension sendet keine Daten an externe Server des Entwicklers und verwendet keine Analyse-, Tracking- oder Werbedienste.

Discord erhält die daraus erzeugten Rich-Presence-Informationen über die lokal laufende Companion-App und die Discord Desktop-App.

## Speicherung

Der Security Token und die Companion-URL werden über `chrome.storage.local` lokal im Browserprofil gespeichert. Playback-Daten werden in der Extension nur für den aktuellen Status zwischengespeichert und nicht dauerhaft archiviert.

## Berechtigungen

- `storage`: speichert Token und Companion-URL lokal.
- `tabs`: findet offene YouTube-Music-Tabs, um Player-Kommandos wie Play/Pause weiterzuleiten.
- `https://music.youtube.com/*`: liest Playback-Daten nur auf YouTube Music.
- `http://127.0.0.1/*` und `http://localhost/*`: verbindet sich mit dem lokalen Companion.

## Kontakt

Probleme, Fragen oder Datenschutz-Hinweise können über GitHub Issues im Repository gemeldet werden:

https://github.com/Kentarohakase/YTMPresence/issues
