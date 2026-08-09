# Chrome Web Store Vorbereitung

Dieses Dokument enthält vorbereitete Texte für eine spätere Veröffentlichung der YTMPresence Extension im Chrome Web Store oder in kompatiblen Browser-Stores.

## Store-Name

YTM Discord Presence Bridge

## Kurzbeschreibung

Zeigt YouTube Music als Discord Rich Presence. Benötigt die kostenlose YTMPresence-Companion-App für Windows.

Die Kurzbeschreibung wird aus `appDescription` in `_locales/<sprache>/messages.json` übernommen und muss dort gepflegt werden, nicht im Store-Dashboard.

## Beschreibung

**Diese Extension benötigt zusätzlich die kostenlose YTMPresence-Companion-App für Windows.** Ohne sie funktioniert die Extension nicht — Discord Rich Presence lässt sich aus dem Browser heraus technisch nicht setzen. Die App gibt es hier:

https://github.com/Kentarohakase/YTMPresence/releases

So arbeiten beide zusammen: Die Extension liest den Playback-Status auf YouTube Music aus und sendet ihn über einen lokalen, tokengeschützten WebSocket an die Companion-App auf demselben Rechner. Die App aktualisiert daraus Discord Rich Presence und kann den aktuellen Track in einem Mini-Player anzeigen. Die Daten verlassen das Gerät nicht.

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

`https://music.youtube.com/*`
: Erlaubt der Extension, Playback-Daten ausschließlich auf YouTube Music auszulesen.

`http://127.0.0.1/*` und `http://localhost/*`
: Erlaubt die Verbindung zur lokal laufenden Companion-App.

### Eingereichter Wortlaut

Das Dashboard verlangt pro Berechtigungsgruppe ein eigenes Feld, jeweils höchstens 1.000 Zeichen und ohne Markdown-Formatierung. Der folgende Wortlaut wurde eingereicht und lässt sich für Edge Add-ons und AMO wiederverwenden.

**Feld „Begründung für storage"**

> Die Erweiterung speichert genau zwei Werte im Browserprofil: das Security-Token der lokalen Companion-App und deren WebSocket-URL. Beide gibt der Nutzer einmalig auf der Optionsseite ein.
>
> Ohne chrome.storage müsste er Token und URL nach jedem Neustart des Browsers erneut eintragen, da die Verbindung sonst nicht hergestellt werden kann.
>
> Es werden keine Wiedergabedaten, keine Verlaufsdaten und keine personenbezogenen Daten gespeichert.

**Feld „Begründung für Hostberechtigung"**

> https://music.youtube.com/*
>
> Das Content-Script liest auf dieser Seite den Wiedergabestatus aus: Titel, Interpret, Album, Position und Dauer. Das ist die einzige Datenquelle der Erweiterung. Über dieselbe Berechtigung findet sie offene YouTube-Music-Tabs, um Play/Pause- und Skip-Befehle aus dem Mini-Player der Companion-App an den richtigen Tab weiterzuleiten.
>
> http://127.0.0.1/* und http://localhost/*
>
> Die Erweiterung verbindet sich per WebSocket mit der YTMPresence-Companion-App, die auf demselben Gerät läuft (Standardport 17373). Nur diese App kann Discord Rich Presence setzen; aus dem Browser heraus ist das technisch nicht möglich. Die Verbindung ist durch ein 256-Bit-Token geschützt.
>
> Die Daten verlassen das Gerät nicht. Es gibt keine Verbindung zu Servern des Entwicklers und keine Analyse- oder Werbedienste.

Host-Berechtigungen lösen bei Google grundsätzlich eine gründlichere Prüfung aus; die entsprechende Warnung im Dashboard ist kein Fehler.

Bewusst **nicht** angefordert wird `tabs`. Die Extension findet YouTube-Music-Tabs allein über die Host-Berechtigung für `https://music.youtube.com/*` und liest aus den Treffern ausschließlich die Tab-ID. Mit `tabs` würde Chrome bei der Installation zusätzlich „Deinen Browserverlauf lesen" anzeigen, ohne dass die Extension davon Gebrauch macht.

## Veröffentlichungsschritte

1. Release-ZIP bauen: `.\scripts\package-release.ps1`
2. `artifacts\release\YTMPresence-extension.zip` im Store hochladen.
3. Store-Name, Kurzbeschreibung und Beschreibung aus diesem Dokument übernehmen.
4. Privacy-Hinweis als URL im Store-Dashboard hinterlegen.
5. Screenshots aus Popup, Optionsseite, Mini-Player und Discord Presence hinzufügen.
6. Prüfen, ob Version in `YTMPresence/extension/manifest.json` zur Release-Version passt.
7. Die vollständige [Store-Submission-Checkliste](STORE_SUBMISSION_CHECKLIST.md) abhaken.

## Noch offen vor Store-Upload

- Store-Screenshots erstellen.
- Optional ein eigenes Promo-Bild für die Store-Seite erstellen.
- Privacy-Text vor öffentlicher Veröffentlichung final prüfen.
