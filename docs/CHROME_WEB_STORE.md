# Chrome Web Store

**Published:** https://chromewebstore.google.com/detail/ytm-discord-presence-brid/obdinnbalnbmpkcaopnhnkdgdkcgligi

Version 1.3.1 was the first approved submission. The extension ID is fixed at `obdinnbalnbmpkcaopnhnkdgdkcgligi`. The companion's existing `chrome-extension:` origin check accepts it without configuration changes.

These submitted texts can also be used for Microsoft Edge Add-ons and addons.mozilla.org, both of which are free. For Firefox, upload `YTMPresence-extension-firefox.zip` instead of the Chromium ZIP.

For each new version, upload the package through the store dashboard. A GitHub release alone does not reach store users.

> **Note on listing language.** The listing was first submitted in German, while the manifest declares `default_locale: "en"`. An English listing was added with the 1.5.0 upload on 2026-11-02, using the English texts below.

## Store name

YTM Discord Presence Bridge

## Short description

Edit the short description in `appDescription` in `_locales/<language>/messages.json`. The store reads it from the package; it cannot be edited in the dashboard.

- English: `Shows YouTube Music as Discord Rich Presence. Requires the free YTMPresence companion app for Windows.`
- German: `Zeigt YouTube Music als Discord Rich Presence. Benötigt die kostenlose YTMPresence-Companion-App für Windows.`

## Description

### English

**This extension additionally requires the free YTMPresence companion app for Windows.** Without it the extension does nothing — Discord Rich Presence cannot technically be set from a browser. The app is available here:

https://github.com/Kentarohakase/YTMPresence/releases

How the two work together: the extension reads the playback state on YouTube Music and sends it over a local, token-protected WebSocket to the companion app on the same machine. The app turns that into Discord Rich Presence and can show the current track in a mini player. The data does not leave the device.

Features:

- Discord Rich Presence for YouTube Music
- Local connection secured by a security token
- Popup showing companion, token and track status
- Options page with a connection test
- Mini player controls through the local companion
- No external analytics, tracking or advertising services

### German (as submitted)

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

## Privacy statement for the store

The extension reads YouTube Music playback data for Discord Rich Presence and sends it only to the local YTMPresence companion on `localhost` or `127.0.0.1`. It sends no data to developer-operated servers and uses no tracking or advertising services.

Full privacy notice:

https://github.com/Kentarohakase/YTMPresence/blob/main/docs/EXTENSION_PRIVACY.md

## Permission justification

`storage`
: Stores the security token and the local companion URL in the browser profile.

`https://music.youtube.com/*`
: Allows the extension to read playback data on YouTube Music only.

`http://127.0.0.1/*` and `http://localhost/*`
: Allows the connection to the locally running companion app.

### Submitted wording

The dashboard has a separate field for each permission group, limited to 1,000 characters without Markdown. The submitted wording below can also be used for Edge Add-ons and AMO.

**Field "Justification for storage"**

> Die Erweiterung speichert genau zwei Werte im Browserprofil: das Security-Token der lokalen Companion-App und deren WebSocket-URL. Beide gibt der Nutzer einmalig auf der Optionsseite ein.
>
> Ohne chrome.storage müsste er Token und URL nach jedem Neustart des Browsers erneut eintragen, da die Verbindung sonst nicht hergestellt werden kann.
>
> Es werden keine Wiedergabedaten, keine Verlaufsdaten und keine personenbezogenen Daten gespeichert.

English equivalent, for an English listing or for AMO:

> The extension stores exactly two values in the browser profile: the security token of the local companion app and its WebSocket URL. The user enters both once on the options page.
>
> Without chrome.storage they would have to re-enter token and URL after every browser restart, as the connection cannot be established otherwise.
>
> No playback data, no browsing history and no personal data are stored.

**Field "Justification for host permission"**

> https://music.youtube.com/*
>
> Das Content-Script liest auf dieser Seite den Wiedergabestatus aus: Titel, Interpret, Album, Position und Dauer. Das ist die einzige Datenquelle der Erweiterung. Über dieselbe Berechtigung findet sie offene YouTube-Music-Tabs, um Play/Pause- und Skip-Befehle aus dem Mini-Player der Companion-App an den richtigen Tab weiterzuleiten.
>
> http://127.0.0.1/* und http://localhost/*
>
> Die Erweiterung verbindet sich per WebSocket mit der YTMPresence-Companion-App, die auf demselben Gerät läuft (Standardport 17373). Nur diese App kann Discord Rich Presence setzen; aus dem Browser heraus ist das technisch nicht möglich. Die Verbindung ist durch ein 256-Bit-Token geschützt.
>
> Die Daten verlassen das Gerät nicht. Es gibt keine Verbindung zu Servern des Entwicklers und keine Analyse- oder Werbedienste.

English equivalent:

> https://music.youtube.com/*
>
> On this page the content script reads the playback state: title, artist, album, position and duration. This is the extension's only data source. Through the same permission it finds open YouTube Music tabs in order to forward play/pause and skip commands from the companion app's mini player to the correct tab.
>
> http://127.0.0.1/* and http://localhost/*
>
> The extension connects over WebSocket to the YTMPresence companion app running on the same device (default port 17373). Only that app can set Discord Rich Presence; it is not technically possible from a browser. The connection is protected by a 256-bit token.
>
> The data does not leave the device. There is no connection to any server operated by the developer, and no analytics or advertising services.

Host permissions always trigger a more thorough review at Google; the corresponding warning in the dashboard is not an error.

The extension does not request `tabs`. The host permission for `https://music.youtube.com/*` lets it find YouTube Music tabs, and it reads only their IDs from the results. Requesting `tabs` would add Chrome's "Read your browsing history" installation warning for an unused permission.

## Publishing steps

1. Build the release ZIP: `.\scripts\package-release.ps1`
2. Upload `artifacts\release\YTMPresence-extension.zip` to the store.
3. Use the store name, short description and description recorded here.
4. Provide the privacy notice as a URL in the store dashboard.
5. Add screenshots of the popup, options page, mini player and Discord presence.
6. Check that the version in `YTMPresence/extension/manifest.json` matches the release version.
7. Work through the full [store submission checklist](STORE_SUBMISSION_CHECKLIST.md).

## Still open

- Add an English listing alongside the German one.
- Optionally create a dedicated promotional image for the store page.
- Submit to Microsoft Edge Add-ons and addons.mozilla.org; both are free and can reuse the screenshots, icon and justifications.
