# YTMPresence Extension Privacy Notice

Last updated: 2026-10-12

YTMPresence is a browser extension for YouTube Music that sends the current playback state to a companion app running locally. The companion app then displays that state as Discord Rich Presence.

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

In addition, the extension stores locally in the browser:

- the security token for the local companion
- the local WebSocket URL of the companion

## What the data is used for

The data is used solely to send the YouTube Music state to the local YTMPresence companion on your machine. The companion uses it for Discord Rich Presence and for the optional mini player.

## Data sharing

The extension sends the playback data only to `localhost` or `127.0.0.1`, that is, to the locally installed companion app. The extension sends no data to any server operated by the developer and uses no analytics, tracking or advertising services.

Discord receives the resulting Rich Presence information through the locally running companion app and the Discord desktop app.

## Storage

The security token and the companion URL are stored locally in the browser profile through `chrome.storage.local`. The extension keeps the latest playback state for each reporting YouTube Music tab in memory to select one source. Content scripts also retain their latest state in memory so selection can recover after a background-worker restart. Playback data is not archived permanently.

In versions with a built-in consent display, Firefox points out `browsingActivity` and `websiteContent` at installation time, because track metadata and the current YouTube Music URL leave the browser for the local companion. That transfer stays on the device (`localhost`/`127.0.0.1`); YTMPresence does not send it to any remote or analytics service.

## Permissions

- `storage`: stores the token and companion URL locally.
- `https://music.youtube.com/*`: reads playback data on YouTube Music only, and finds open YouTube Music tabs in order to forward player commands such as play/pause.
- `http://127.0.0.1/*` and `http://localhost/*`: connects to the local companion.

The `tabs` permission is deliberately not requested. The extension finds YouTube Music tabs through the host permission alone, uses their IDs and lifecycle events to maintain the selection, and sends player commands only to the selected tab.

## Contact

Problems, questions or privacy concerns can be reported through GitHub Issues in the repository:

https://github.com/Kentarohakase/YTMPresence/issues
