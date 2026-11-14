# YTMPresence Extension Privacy Notice

Last updated: 2026-11-15

The YTMPresence browser extension sends the current YouTube Music playback state to a local companion app. The companion displays it as Discord Rich Presence.

## What data is processed

On `https://music.youtube.com/*` the extension reads the current track's metadata and playback state:

- track title
- artist
- album, if available
- cover URL, if available
- playback state, position and duration
- the current YouTube Music page URL and a generated track or video link
- playback mode and a message timestamp
- whether an advertisement was detected

Connection tests also send the extension's version number so the companion's diagnostics can show it. Playback messages and connection tests include the companion's security token for authentication.

**Copy diagnostics** in the popup builds a text report from the extension's connection status, its version and installation type, and the browser's User-Agent and interface language, and puts it on the clipboard. The report contains no token, track titles or artists. The extension does not send it anywhere; sharing it is up to you.

The extension also stores these settings locally in the browser:

- the security token for the local companion
- the local WebSocket URL of the companion

## What the data is used for

The extension uses this data only to report YouTube Music playback to the local companion. The companion uses it for Discord Rich Presence and the optional mini player.

## Data sharing

The extension sends playback data over a token-protected WebSocket only to the local companion at `localhost` or `127.0.0.1`. Custom companion URLs are restricted to those hosts. This local connection is the first part of the data flow:

Browser extension → local WebSocket → companion app → local Discord IPC → Discord desktop app → Discord service and Rich Presence.

The companion sends a Rich Presence activity through Discord's local IPC connection. Depending on settings and available metadata, it includes:

- track title, artist and album;
- playing or paused state, playback position and duration, and timestamps used to display elapsed or remaining time;
- a track link, which may be a YouTube Music search link, and a link to YouTube Music;
- the track's cover URL as the large activity image, or configured activity images if no suitable cover is available or the option is turned off, and the image labels;
- an advertisement status when that option is enabled.

Discord receives and processes this information outside the device. Other Discord users may see the track details, playback status, timing, images and links on your activity or profile, depending on Discord's [activity-sharing settings](https://support.discord.com/hc/en-us/articles/7931156448919-Activity-Sharing-on-Discord-FAQ). Discord's processing is governed by its [privacy policy](https://discord.com/privacy).

The companion token, companion WebSocket URL and extension version are not included in the Rich Presence activity. By default, the activity passes the track's cover URL to Discord when it points to a YouTube Music image host (`*.ytimg.com`, `*.ggpht.com`, or the `lhN`/`ytN` hosts under `googleusercontent.com`). For music videos, it passes the 16:9 version of the same YouTube thumbnail instead. Discord then loads the image from that URL. The companion setting **Show album art in Discord** turns this off; the activity then uses the configured images. The implemented activity fields are defined in [PresenceService.cs](../YTMPresence.Core/PresenceService.cs) and sent by [DiscordIpcClient.cs](../YTMPresence.Core/DiscordIpcClient.cs).

When displaying a cover, the mini player can request the image from the supplied URL if it points to one of the YouTube Music image hosts listed above; it does not load other cover URLs. The companion also contacts GitHub for update checks and downloads. These requests are separate from the extension's local WebSocket connection.

YTMPresence has no analytics, tracking or advertising integration and sends no data to developer-operated collection servers. This does not describe Discord's own data processing or mean that the complete workflow stays on the device.

## Storage

The security token and the companion URL are stored locally in the browser profile through `chrome.storage.local`. The extension keeps the latest playback state for each reporting YouTube Music tab in memory to select one source. Content scripts also retain their latest state in memory so selection can recover after a background-worker restart. The extension does not permanently archive playback data.

Firefox versions with a built-in consent display list `browsingActivity` and `websiteContent` during installation because track metadata and the current YouTube Music URL are sent from the browser to the local companion. This transfer stays on the device (`localhost`/`127.0.0.1`); the extension does not send it to a remote or analytics service.

## Permissions

- `storage`: stores the token and companion URL locally.
- `https://music.youtube.com/*`: reads playback data only on YouTube Music and finds open YouTube Music tabs to forward player commands such as play/pause.
- `http://127.0.0.1/*` and `http://localhost/*`: connects to the local companion.

The extension does not request the `tabs` permission. It finds YouTube Music tabs through the host permission, uses their IDs and lifecycle events to maintain the selection, and sends player commands only to the selected tab.

## Contact

Report problems, questions or privacy concerns through GitHub Issues:

https://github.com/Kentarohakase/YTMPresence/issues
