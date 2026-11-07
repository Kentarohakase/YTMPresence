# YTMPresence Extension Privacy Notice

Last updated: 2026-10-12

The YTMPresence browser extension sends the current YouTube Music playback state to a local companion app. The companion displays it as Discord Rich Presence.

## What data is processed

On `https://music.youtube.com/*` the extension reads the visible playback data of the currently playing track:

- track title
- artist
- album, if available
- cover URL, if available
- playback state, position and duration
- track or video link
- whether an advertisement was detected
- the extension's version number, so that the companion's diagnostics can show it

The extension also stores these settings locally in the browser:

- the security token for the local companion
- the local WebSocket URL of the companion

## What the data is used for

The extension uses this data only to report YouTube Music playback to the local companion. The companion uses it for Discord Rich Presence and the optional mini player.

## Data sharing

Playback data is sent only to the local companion at `localhost` or `127.0.0.1`. The extension sends no data to developer-operated servers and uses no analytics, tracking or advertising services.

Discord receives the resulting Rich Presence information through the locally running companion app and the Discord desktop app.

## Storage

The security token and the companion URL are stored locally in the browser profile through `chrome.storage.local`. The extension keeps the latest playback state for each reporting YouTube Music tab in memory to select one source. Content scripts also retain their latest state in memory so selection can recover after a background-worker restart. Playback data is not archived permanently.

Firefox versions with a built-in consent display list `browsingActivity` and `websiteContent` during installation because track metadata and the current YouTube Music URL are sent from the browser to the local companion. This transfer stays on the device (`localhost`/`127.0.0.1`); the extension does not send it to a remote or analytics service.

## Permissions

- `storage`: stores the token and companion URL locally.
- `https://music.youtube.com/*`: reads playback data only on YouTube Music and finds open YouTube Music tabs to forward player commands such as play/pause.
- `http://127.0.0.1/*` and `http://localhost/*`: connects to the local companion.

The extension does not request the `tabs` permission. It finds YouTube Music tabs through the host permission, uses their IDs and lifecycle events to maintain the selection, and sends player commands only to the selected tab.

## Contact

Report problems, questions or privacy concerns through GitHub Issues:

https://github.com/Kentarohakase/YTMPresence/issues
