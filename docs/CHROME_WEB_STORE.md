# Chrome Web Store

**Store listing:** https://chromewebstore.google.com/detail/ytm-discord-presence-brid/obdinnbalnbmpkcaopnhnkdgdkcgligi

Version 1.3.1 was the first approved submission. The extension ID is fixed at `obdinnbalnbmpkcaopnhnkdgdkcgligi`. The companion's existing `chrome-extension:` origin check accepts it without configuration changes.

The texts below include local data-flow corrections from 2026-11-07 and are no longer a verbatim record of earlier submissions. Editing this file does not update the published listing. See the dated [release status](STORE_SUBMISSION_CHECKLIST.md#release-status) for confirmed publication and recorded submissions.

These texts can also be used for Microsoft Edge Add-ons and addons.mozilla.org, both of which are free. For Firefox, upload `YTMPresence-extension-firefox.zip` instead of the Chromium ZIP.

For each new version, upload the package through the store dashboard. A GitHub release alone does not reach store users.

> **Note on listing language.** The listing was first submitted in German, while the manifest declares `default_locale: "en"`. The 2026-11-02 record says an English listing was added to the 1.5.0 submission. This records a submission, not confirmation that it has been published.

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

The extension reads the current track's title, artist, album, cover URL, playback state, timing, links and advertisement status on YouTube Music. It sends these over a local, token-protected WebSocket to the companion app, which can show the track in a mini player.

The companion passes track details, playback status, timing and links to the local Discord desktop app for Rich Presence. Discord processes this information outside the device and may display it to other users according to your Discord activity-sharing settings. The mini player can also load cover images from the supplied image URL. YTMPresence sends no data to developer-operated collection servers.

Features:

- Discord Rich Presence for YouTube Music
- Local connection secured by a security token
- Popup showing companion, token and track status
- Options page with a connection test
- Mini player controls through the local companion
- No external analytics, tracking or advertising services

<a id="german-as-submitted"></a>

### German

**Diese Extension benötigt zusätzlich die kostenlose YTMPresence-Companion-App für Windows.** Ohne sie funktioniert die Extension nicht — Discord Rich Presence lässt sich aus dem Browser heraus technisch nicht setzen. Die App gibt es hier:

https://github.com/Kentarohakase/YTMPresence/releases

Die Erweiterung liest auf YouTube Music Titel, Interpret, Album, Cover-URL, Wiedergabestatus, Zeitangaben, Links und Werbestatus des aktuellen Tracks. Sie sendet diese Daten über einen lokalen, tokengeschützten WebSocket an die Companion-App, die den Track in einem Mini-Player anzeigen kann.

Für Rich Presence übergibt der Companion Track-Details, Wiedergabestatus, Zeitangaben und Links an die lokale Discord-Desktop-App. Discord verarbeitet diese Informationen außerhalb des Geräts und kann sie anderen Nutzern entsprechend deinen Discord-Aktivitätseinstellungen anzeigen. Der Mini-Player kann außerdem Cover von der übermittelten Bild-URL laden. YTMPresence sendet keine Daten an Server des Entwicklers.

Funktionen:

- Discord Rich Presence für YouTube Music
- Lokale Verbindung über Security Token
- Popup mit Companion-, Token- und Track-Status
- Optionsseite mit Verbindungstest
- Mini-Player-Steuerung über den lokalen Companion
- Keine externen Analyse-, Tracking- oder Werbedienste

## Privacy statement for the store

The extension sends YouTube Music metadata and playback state only to the local YTMPresence companion on `localhost` or `127.0.0.1`. The companion passes title, artist, album, playback state, timing and links to the Discord desktop app. Discord receives this information outside the device and may show it to other users according to your activity-sharing settings. The companion token is not part of the Rich Presence activity.

The mini player can fetch cover images from the supplied URL, and companion update checks and downloads contact GitHub. YTMPresence has no analytics, tracking or advertising integration and sends no data to developer-operated collection servers.

Full privacy notice:

https://github.com/Kentarohakase/YTMPresence/blob/main/docs/EXTENSION_PRIVACY.md

## Permission justification

`storage`
: Stores the security token and the local companion URL in the browser profile.

`https://music.youtube.com/*`
: Allows the extension to read playback data on YouTube Music only.

`http://127.0.0.1/*` and `http://localhost/*`
: Allows the connection to the locally running companion app.

<a id="submitted-wording"></a>

### Submission wording

The dashboard has a separate field for each permission group, limited to 1,000 characters without Markdown. The revised wording below can also be used for Edge Add-ons and AMO.

**Field "Justification for storage"**

> Die Erweiterung speichert genau zwei Werte im Browserprofil: das Security-Token der lokalen Companion-App und deren WebSocket-URL. Beide gibt der Nutzer einmalig auf der Optionsseite ein.
>
> Ohne chrome.storage müsste er Token und URL nach jedem Neustart des Browsers erneut eintragen, da die Verbindung sonst nicht hergestellt werden kann.
>
> Wiedergabedaten und Verlauf werden nicht dauerhaft im Browserprofil gespeichert.

English equivalent, for an English listing or for AMO:

> The extension stores exactly two values in the browser profile: the security token of the local companion app and its WebSocket URL. The user enters both once on the options page.
>
> Without chrome.storage they would have to re-enter token and URL after every browser restart, as the connection cannot be established otherwise.
>
> Playback data and browsing history are not written to browser storage.

**Field "Justification for host permission"**

> https://music.youtube.com/*
>
> Das Content-Script liest Titel, Interpret, Album, Cover-URL, Track-Link, Wiedergabestatus, Position, Dauer und Werbestatus. Die Berechtigung dient auch dazu, YouTube-Music-Tabs für Play/Pause- und Skip-Befehle aus dem Mini-Player zu finden.
>
> http://127.0.0.1/* und http://localhost/*
>
> Die Erweiterung sendet diese Daten per WebSocket nur an den lokalen Companion (Standardport 17373), geschützt durch ein 256-Bit-Token. Der Companion übergibt Titel, Interpret, Album, Wiedergabestatus, Zeitangaben und Links über die lokale Discord-Desktop-App an Discord.
>
> Discord verarbeitet diese Daten außerhalb des Geräts und kann sie anderen Nutzern gemäß den Aktivitätseinstellungen anzeigen. YTMPresence sendet keine Daten an Entwickler-Server und enthält keine Analyse-, Tracking- oder Werbedienste.

English equivalent:

> https://music.youtube.com/*
>
> The content script reads title, artist, album, cover URL, track link, playback state, position, duration and advertisement status. This permission also lets it find YouTube Music tabs for play/pause and skip commands from the mini player.
>
> http://127.0.0.1/* and http://localhost/*
>
> The extension sends this data only to the local companion over WebSocket (default port 17373), protected by a 256-bit token. The companion passes title, artist, album, playback state, timing and links through the local Discord desktop app to Discord.
>
> Discord processes this information outside the device and may show it to other users according to activity-sharing settings. YTMPresence sends no data to developer-operated servers and has no analytics, tracking or advertising integration.

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

- Verify publication of the English listing recorded with the 1.5.0 submission; do not treat submission as publication.
- Apply the corrected English and German data-flow wording and review the privacy declarations in a separately authorized store update.
- Optionally create a dedicated promotional image for the store page.
- Submit to Microsoft Edge Add-ons and addons.mozilla.org; both are free and can reuse the screenshots, icon and justifications.
