# Changelog

All notable changes to YTMPresence are documented in this file.

Dates for published GitHub releases use Europe/Vienna time. The [GitHub release list](https://github.com/Kentarohakase/YTMPresence/releases), checked on 2026-11-07, contains no releases for 0.7.0, 0.8.11 or 1.0.0; their existing entries and dates are retained as project history.

## [1.6.1] - 2026-11-15

### Added

- The extension popup has a **Copy diagnostics** button. It copies a plain-text report with the extension and companion versions, installation type, browser, connection state and the time of the last track update, without the token, track titles or artists. If the browser blocks the clipboard, the popup shows the text for copying by hand.
- Popup and options show a notice with a link to the GitHub releases when the connected companion is older than 1.5.1 or does not report its version.
- After a fresh install without a saved token, the extension opens its options page with a short welcome and the four setup steps. Updates and installations that already have a token are left alone.

### Fixed

- The mini player loads a cover only from the YouTube Music image hosts that Discord presence already accepts (`*.ytimg.com`, `*.ggpht.com` and the `lhN`/`ytN` hosts under `googleusercontent.com`). It used to request any HTTPS address the page reported as the cover, so a script on the YouTube Music page could make the companion contact arbitrary servers, including hosts on the local network. For other URLs the mini player shows its placeholder instead.

### Changed

- While a track plays, the Discord status line shows artist and album instead of "Artist · 1:23 / 3:45". Discord already draws a progress bar with both times from the activity's timestamps, so the text repeated them. Paused tracks keep "Paused at 1:23". The companion no longer resends the presence every 15 seconds just to refresh that text; it updates on track changes, play/pause, a new cover, and seeks or stalls of more than three seconds.
- After Discord restarts or the connection drops, the companion sends the presence again with the next playback update from the extension. This used to happen as a side effect of the periodic time updates. A paused track still returns only after its state changes, as before.

### Notes

- The extension changes in this release (**Copy diagnostics**, the companion version notice and the welcome page) reach store users only with a separate store upload; GitHub and the Chrome Web Store update independently. The extension requests no new permissions.

## [1.6.0] - 2026-11-15

### Added

- Discord presence shows the current track's album art as its large image, as long as YouTube Music provides a cover. Without a cover, or if the URL is not from a YouTube Music image host or exceeds Discord's 300-character limit, the YouTube Music logo appears as before. The cover carries no play/pause icon, since the icon would hide part of it; the state line still says when playback is paused, and the icon stays with the logo. For music videos, YouTube Music provides a 4:3 video thumbnail with black bars instead of a cover; Discord receives the 16:9 version of the same thumbnail (`mqdefault`, 320×180) without those bars. It is less sharp than an album cover, but exists for every video. **Show album art in Discord** in the settings window turns the cover off; existing settings files start with it enabled. Discord loads the image itself from the cover URL, see the [privacy notice](docs/EXTENSION_PRIVACY.md#data-sharing).

### Fixed

- When several browsers or profiles are connected, Discord presence, the mini player and its controls now use one selected source. Paused sources cannot replace a playing one, and commands reach only that source. Selection follows the same rules as within one browser: keep the playing source; on pause, switch only if another is playing; on disconnect, choose an available source immediately. The extension protocol is unchanged, so older extensions also benefit.
- The extension no longer cuts cover URLs to 256 characters. A shortened URL pointed to no valid image; URLs over 2,048 characters are now omitted. Because older extensions still shorten them, the companion does not pass cover URLs of exactly 256 characters to Discord.

### Changed

- The diagnostics window and report identify the selected source, for example "Edge 155 #3".
- When Discord closes the connection, diagnostics include its reason, for example "Invalid Client ID (code 4000)".
- If the companion's port is occupied, a notification explains how to resolve it and the tray menu shows "Server: port 17373 in use". Previously, the menu showed only "Server: stopped", with the reason available in the log.
- When the Discord desktop app is closed, the tray menu shows "Discord: not running". Diagnostics explain that the companion will connect automatically when Discord starts, replacing the English IPC error message.
- The extension includes its version in the connection test, and diagnostics show it next to the selected source. Older companions ignore the field; older extensions do not report a version.

### Notes

- The browser extension changes in this release (complete cover URLs, version in the connection test) reach store users only with a separate store upload; GitHub and the Chrome Web Store update independently. The extension requests no new permissions. Album art in Discord also works with older extensions, except for covers whose URL they shortened.

## [1.5.1] - 2026-11-02

### Fixed

- The first presence update after starting the companion or Discord is no longer lost. The companion now waits for Discord's READY message before sending commands. Previously, Discord dropped the update sent immediately after the handshake; a paused track could remain missing until its details changed.

### Changed

- The tray menu, diagnostics window and report show Discord's response to each activity update: accepted, rejected with a code and reason, or no response within three seconds. Previously, sending an update counted as success, even if Discord rejected it; rejections appeared only in the log.

### Notes

- The browser extension is unchanged apart from its version number, which follows the companion's. The store record from 2026-11-02 lists 1.5.0 as submitted and in review, with no 1.5.1 upload. This does not confirm store publication; see the dated [release status](docs/STORE_SUBMISSION_CHECKLIST.md#release-status).

## [1.5.0] - 2026-11-02

### Added

- A four-step setup guide with Chrome/Edge store and Firefox links, a separate developer section, manual token copying and feedback in German and English. The guide shows the companion version; the extension popup and options show the extension version and installation type without requesting additional permissions.
- Optional `companionVersion` in `YTM_TEST_RESULT`. Older peers remain compatible; missing versions show “Not reported”, and mismatched versions do not block the connection.
- Extension tests run in CI and the release workflow using Node's built-in test runner. They cover tab selection, targeted commands, tab lifecycle, background restarts, pending state, playback extraction and track URLs without contacting YouTube Music or Discord.
- `scripts/test-clean-install.ps1` tests install, start, update and uninstall in Windows Sandbox, which has no .NET and no network.

### Changed

- The setup and ZIP now include .NET. Earlier packages required both the .NET 10 Desktop Runtime and the ASP.NET Core Runtime, but neither the setup nor the README listed both; without the ASP.NET Core Runtime, the companion would not start. The setup size increases from 2.2 MB to about 62 MB.
- Extension options now use **Save and check**. The background script coordinates saving and connection changes; popup retries use the saved configuration on the same connection. Custom URLs are under Advanced settings. Status messages distinguish storage failures, an unreachable companion and a rejected token, and explain that each browser profile and installation has separate settings.
- The Firefox package requires Firefox 140 or later instead of 126. Earlier versions do not support `data_collection_permissions`, which the package uses to declare its data use.
- The release workflow verifies the extension archives and lints the Firefox package before publishing, as CI already did. Both workflows run on Node 24.

### Fixed

- Within one browser profile, a playing YouTube Music tab stays selected for presence and mini-player commands. Paused background tabs no longer replace it, and commands go only to the selected tab's main frame. Pausing keeps the selection unless another tab is playing. Closing, reloading, navigating away from or discarding the tab selects a fallback in stable tab-ID order, preferring playing tabs.
- Autoplay progress uses the current track's player-bar timeline when available. YouTube Music can reuse a cumulative media timeline across tracks, which previously added the previous track's elapsed time to the new track in the mini player and Discord. The media element remains the fallback when player-bar values are invalid or hidden.
- The extension badge turns green only after the companion accepts the token on the current socket. Connection checks take up to four seconds, and reconnect delays are bounded; both ignore obsolete sockets and configurations. After reconnecting and authenticating, the extension resends the selected track. Keepalive replies neither resend playback nor reset pause or idle state. Popup and options use the same status messages and next steps, and distinguish a companion connection from visible Discord presence.
- Background-worker restarts recover playback state from open tabs, including paused tracks. Closed tabs cannot be restored by late recovery responses or replayed after a token change, and obsolete WebSocket connections cannot deliver player commands.
- Update errors no longer repeat their prefix, as in „Update check failed: Update-Check fehlgeschlagen: HTTP 404.“. Core messages for update checks, downloads and setup launch are now English, matching the log; surrounding interface text still follows the Windows language. The first log line is also English.
- The settings, diagnostics and mini-player windows now follow the Windows language throughout. Previously, status and validation messages such as „Token kopiert.“, „Host darf nicht leer sein.“ and „Keine Extension verbunden“ stayed German, and diagnostics mixed German and English labels. The diagnostics report remains English, with its two German lines translated. Discord connection errors in diagnostics are also English.

### Known limitations

- With several browsers or browser profiles connected at the same time, the companion still sends mini-player commands to all of them. Selecting one source across connections is planned for 1.6.0. The companion protocol and the idle-clear behavior are unchanged.

## [1.4.0] - 2026-08-10

### Changed

- The changelog, including earlier entries, the README and all documentation under `docs/` are now in English for the extension's international audience.

### Fixed

- Discord presence text now follows the system language. Playback state, paused position, advertisement notices and the track button shown to other Discord users previously stayed German, including „Pausiert bei 1:23" and „Auf YouTube Music öffnen". `PresenceText` now translates these strings, with English as the fallback.
- The diagnostics and getting-started windows now translate server state, connection and Discord status, age labels, update-check status and errors when opening YouTube Music. These strings previously stayed German regardless of the system language.
- Log messages are now English and are not localized, so reported messages match the source and can be searched consistently. The diagnostics window continues to follow the system language.
- The mini player follows the Windows language. Playback state, the no-track placeholder and the label of the previous-track command were hard-coded German and stayed German even when the rest of the interface ran in English.

## [1.3.1] - 2026-08-09

### Changed

- The extension no longer requests the unused `tabs` permission, removing Chrome's "Read your browsing history" warning at installation. `tabs.query` with a URL filter and `tabs.sendMessage` use the existing host permission for `https://music.youtube.com/*`; `tabs.create` needs no permission. Only tab IDs are read from query results.
- The short description and store text now state the companion app requirement first, since a browser cannot set Discord Rich Presence. Chrome reads the short description from `_locales/<language>/messages.json` through the manifest, not from the store dashboard.
- `docs/STORE_SUBMISSION_CHECKLIST.md` now contains the data-collection answers for the Chrome dashboard, aligned with the Firefox metadata already declared.

## [1.3.0] - 2026-08-09

### Fixed

- Discord presence clears again on pause, timeout and shutdown. The client used the unsupported `CLEAR_ACTIVITY` command, discarded Discord's rejection and reported "Discord OK". This prevented `OnlyShowWhenPlaying` and `PausedClearSeconds` from working. Clearing now uses `SET_ACTIVITY` with `activity: null`.
- Track URLs retain their percent encoding. URL normalization used `Uri.ToString()` in three places, turning `?q=Sam%20Smith` into `?q=Sam Smith`. Discord rejected the entire activity with `"url" must be a valid uri`. This affected fallback search links when no video ID was available. The code now uses `AbsoluteUri`.
- Invalid button URLs no longer prevent the presence from appearing. The URL is validated before the button is built; an invalid button is omitted and the reason is logged.
- Track and play/pause updates no longer clear the presence first or wait 200 ms before sending it. `SET_ACTIVITY` replaces the activity directly. Removing the old "Discord cache fix" avoids a brief disappearance now that clearing works; the previous `CLEAR_ACTIVITY` command had been ignored.
- Error responses from Discord are now logged. Previously, discarded responses hid the unsupported clear command.

### Added

- Diagnostics now show the last successful update check and flag checks older than two weeks or disabled startup checks. The timestamp is saved in settings and included in the diagnostics report. Failed startup checks still appear only in the log, avoiding notifications when the machine starts offline.

## [1.2.0] - 2026-08-09

### Added

- `scripts/verify-extension-archives.ps1` checks built extension archives for invalid path separators, absolute paths and path traversal, extracts them to disk, and resolves every `__MSG_*__` placeholder against the default-language catalogue.
- CI now builds release artifacts with `package-release.ps1 -SkipInstaller`, verifies them, and lints the extracted Firefox archive to catch packaging defects that source-folder checks miss.

### Changed

- The companion WebSocket now checks the handshake's `Origin`. Previously, websites could connect to the local endpoint and make unlimited token guesses through `YTM_TEST`, since WebSockets are not restricted by the same-origin policy. The 256-bit token protected access, but the endpoint remained available for fingerprinting. Extension origins (`chrome-extension:`, `moz-extension:`, `safari-web-extension:`) and a missing origin for local non-browser clients are allowed; web origins receive HTTP 403.
- Sockets are closed if they do not present a valid token within 60 seconds. Connection tests with a valid token count as authentication, allowing the extension to keep the connection alive while playback is paused.
- The matching instance-key implementations, `InstallerCoordination.GetInstanceKey` and `MakeInstanceKey` in `installer/YTMPresence.iss`, now reference each other and share a test vector. `docs/RELEASE_QA.md` adds a separate self-shutdown check to verify that both produce the same key. A mismatch can go unnoticed because the installer falls back to the Restart Manager and still completes the update.
- Setup selection now takes the file name and download URL from a single asset. Releases with multiple `-setup.exe` assets are rejected as ambiguous, like conflicting checksum lines, rather than relying on GitHub's unspecified response order.
- Releases marked `prerelease: true` are no longer offered as updates. `/releases/latest` already filters them, but `UpdateApiUrl` is configurable.
- Checksum asset selection and verification now both use ordinal file-name comparison. A differently cased `sha256sums.txt` is rejected during selection rather than later with a misleading security message. Artifact names are defined centrally in `UpdateArtifactNames`.

### Fixed

- Pausing playback is detected correctly again, allowing `OnlyShowWhenPlaying` and `PausedClearSeconds` to clear the presence. YouTube Music does not reliably update `navigator.mediaSession.playbackState` on pause, so the extension now reads the media element's `paused` property first and uses `mediaSession` only as a fallback. Updates during seeks (`seeking`) are skipped to avoid flicker.
- Media detection now includes `<video>`. Previously, pages without an `<audio>` element had no source for position, duration or playback state and defaulted to "playing".
- Presence links no longer point to unrelated videos in the queue, recommendations or sidebar. Unscoped selectors in `getPlayerBarWatchUrl` (`a[href*="/watch?v="]` and `a[href*="music.youtube.com/watch?v="]`) could select unrelated links. Anchor lookup is now limited to `<ytmusic-player-bar>`, and per-track artwork takes priority when choosing the URL because it reliably identifies the current track.
- Track URL detection now checks the address bar. As of 2026-08, YouTube Music exposes neither player-bar anchors nor `canonical`, while `og:url` points to `/app_shell` without a video ID. Artwork was the only working source. Candidates are now checked in this order: artwork, player bar, address bar, canonical. Identical branches for `video` and default mode were merged.
- Both extension ZIPs and the bundle ZIP now use forward slashes as path separators. Windows PowerShell 5.1's `Compress-Archive` wrote backslashes, causing ZIP-compliant store extractors, including the Chrome Web Store and addons.mozilla.org, to treat paths such as `_locales\en\messages.json` as single file names. This prevented manifest `__MSG_*__` placeholders from resolving.
- `verify-release.ps1` now rejects archive entries containing backslashes and lists them in the error. Previously, it normalized those paths before checking, hiding the packaging defect.

## [1.1.0] - 2026-08-05

### Added

- Version consistency check across app, assembly metadata, extension and release changelog; CI and release packaging abort on any mismatch.
- Settings recovery preserves corrupt JSON files as `settings.corrupt-<timestamp>.json` and then creates valid defaults.
- Update security components in `YTMPresence.Core.Updates` for URL validation, checksum parsing, size limits, temporary working directories, downloads and launch verification, with tests for checksum, download, redirect and process-start cases.
- Settings flag `AllowLoopbackUpdateSource` (default: off) for testing with the local QA update server. `scripts/test-update-flow.ps1` enables it automatically and restores the original settings afterwards.

### Changed

- Settings are saved to a temporary file, then written with an atomic file replacement.
- Firefox packages declare the local transfer of browsing activity and website content through Mozilla's consent metadata, without raising the existing minimum version of 126.
- Updates require successful SHA256 verification. Installation is blocked if `SHA256SUMS.txt` is missing, its setup entry is missing, duplicated or conflicting, or the hash is invalid or does not match. Updates can no longer skip checksum verification.
- Update sources are restricted to HTTPS on `api.github.com`, `github.com`, `release-assets.githubusercontent.com` and `objects.githubusercontent.com`. Metadata and asset redirects are checked individually (max. 5, with loop detection). Downloads have size limits (setup 300 MB, checksums 64 KB, metadata 4 MB) and connection, stall and overall timeouts. Custom `UpdateApiUrl` values outside this policy are rejected with a clear error message.
- Each update attempt uses a separate temporary directory. Downloads use a `.part` file that is renamed atomically after verification. Errors and cancellations trigger cleanup; orphaned directories are removed by age when the app starts.
- Setup files are checked again for existence, name, size and SHA256 immediately before launch, and locked against modification during launch. YTMPresence exits only after setup starts and stays usable if launch fails.
- The Inno Setup installer asks the installed instance to shut down cleanly through a shutdown event bound to the target path with a collision-resistant SHA256. If that fails, Windows Restart Manager (`CloseApplications=force`) closes only processes holding files in the installation folder. It no longer uses `taskkill` to terminate processes by file name. Portable copies and unrelated processes with the same name are left alone.
- `verify-release.ps1` checks that `SHA256SUMS.txt` exists and has exactly one valid line per artifact, correct hashes, and no stale, duplicate or unknown lines. It does not rewrite the file. Only `package-release.ps1` creates the list, after all artifacts are final. The release workflow builds and tests the solution before packaging.

### Fixed

- WebSocket text messages stay intact when a UTF-8 character is fragmented across several frames; binary and oversized messages are still discarded.
- Server start, stop and presence updates are serialized. Restarts shut down idle tasks and release Discord resources.
- A backwards-compatible keepalive over the existing connection test keeps the Manifest V3 WebSocket connection alive while playback is paused, without resetting the pause or idle timeouts.
- The final release checksum list now also contains the Firefox ZIP and, if built, the setup EXE.

## [1.0.0] - 2026-06-20

### Added

- Test project `YTMPresence.Core.Tests` (xUnit) with tests for the security token, settings normalisation, update check (SemVer/asset selection), presence logic and CompanionServer token authentication.
- PR CI workflow (`.github/workflows/ci.yml`): build, tests and `web-ext lint` on pull requests and pushes to `main`.
- English localisation of the browser extension through `chrome.i18n` (`_locales/en` as default, `_locales/de`); popup, options and manifest are now DE/EN.
- Firefox support: the release pipeline additionally produces `YTMPresence-extension-firefox.zip` with `browser_specific_settings`.

### Changed

- The tray windows (getting started, settings, mini player, update) now use the central `UiText` strings for German and English text.
- When Discord is not running, the IPC client waits briefly between connection attempts and reconnects automatically. Previously, it tried every pipe for each message.
- Release verification checks the new extension files (`i18n.js`, `_locales`) and both extension ZIPs (Chromium + Firefox).

### Removed

- Removed the unused `YTMPresence.Tray` project, which was not part of the solution.

## [0.8.11] - 2026-06-01

### Fixed

- The Inno Setup update now terminates only YTMPresence, leaving the setup launched from it running.

## [0.8.10] - 2026-05-15

### Changed

- The release script now uses only Inno Setup for installer builds, simplifying maintenance.

### Fixed

- The release script removes stale setup artifacts and no longer leaves an old setup EXE behind when `-SkipInstaller` is used.
- The update window disables the download and install buttons when a release contains no setup asset.
- The companion server discards non-text and oversized WebSocket messages early.
- The settings reject URL-like hosts and invalid WebSocket paths.

### Removed

- The old IExpress-based installer fallback was removed.

## [0.8.9] - 2026-05-15

### Added

- Inno Setup installer pipeline with GitHub Actions integration for release builds.

### Changed

- The release script prefers Inno Setup. Local builds fall back to the legacy installer when `ISCC.exe` is missing.

## [0.8.8] - 2026-05-15

### Added

- The diagnostics window can now export a ZIP report containing status, environment, redacted settings and logs.

## [0.8.7] - 2026-05-15

### Fixed

- The update installer now launches setup with an explicit working directory. The app stays open until the installer is visibly running.

## [0.8.6] - 2026-05-15

### Changed

- The update installer can also download and verify setups without exiting the app.

## [0.8.5] - 2026-05-15

### Added

- Update installer window with setup download, progress, optional SHA256 check and subsequent setup launch.

### Changed

- The update notice now points at installing directly through the tray menu.

## [0.8.4] - 2026-05-15

### Added

- Getting-started window with token, companion URL, YouTube Music link and direct access to the settings.
- Release QA checklist for installation, update, uninstallation and GitHub release checks.

### Changed

- For available releases, the update menu opens the setup EXE directly when GitHub provides a matching installer asset.
- Local `.lscache` build files are ignored.

### Fixed

- The installer waits for the setup EXE produced by IExpress to become readable before computing SHA256 checksums.
- The mini player tooltip uses the correct umlaut in `Track öffnen` again.

## [0.8.3] - 2026-05-15

### Added

- The Windows installer now registers YTMPresence in the Windows app settings with version, icon, size and uninstall command.

### Changed

- Installer and release script check cleanup targets more carefully before removing old artifacts.
- On update, the installer only terminates the installed YTMPresence instance from the installation folder.

### Fixed

- Installation aborts earlier with a clear error message when `YTMPresence.exe` is missing after extraction.
- Uninstallation removes the Windows uninstall entry and the temporary cleanup script.

## [0.8.2] - 2026-05-15

### Added

- User-level Windows installer packaging with setup EXE and start menu shortcut.
- Update check against GitHub Releases directly from the tray menu.
- Store submission checklist for the Chrome Web Store and compatible browser stores.

### Changed

- The release workflow now also uploads the setup artifact.
- The extension manifest adds `short_name` and `homepage_url` for store publication.

## [0.8.1] - 2026-05-15

### Added

- GitHub Actions workflow for automatic tag releases with ZIP artifacts and SHA256 checksums.
- Extension icons at 16, 32, 48 and 128 pixels.
- Privacy notice and Chrome Web Store preparation for the extension.
- `.editorconfig` for UTF-8 files.

### Changed

- App version and extension version raised to `0.8.1`.
- German README, changelog and extension texts use real UTF-8 umlauts.
- Release notes are written as UTF-8 in the package.

## [0.8.0] - 2026-05-15

### Added

- Mini player controls for play/pause, previous and next.
- Return channel from the companion to the extension for player commands.
- New extension options page with live status, connection test, default URL and a YT Music button.
- Release verification through `scripts/verify-release.ps1`.
- Automatic SHA256 checksums in `artifacts/release/SHA256SUMS.txt`.
- Automatic cleanup of old versioned release artifacts in the packaging script.
- GitHub license file and a merged `.gitignore`.

### Changed

- Extension version and app version aligned at `0.8.0`.
- Default mini player width adjusted to the new controls.
- After building, the release script checks that the app, extension and ZIP contents are complete.

### Fixed

- The release bundle now copies the app files into the `app` folder correctly.
- An automatic ZIP structure check prevents incomplete releases.

## [0.7.0] - 2026-05-14

### Added

- Extension popup with companion status, token status, URL and last track.
- Badge on the extension icon for connection status.
- Connection test directly in the popup.
- Faster state updates on play/pause, seek and YouTube Music navigation.

### Changed

- Improved injection guards for the content and page scripts.
- Extension README notes on the popup and badge added.

## Before 0.7.0

### Added

- Local WPF tray app as a Discord Rich Presence companion.
- Browser extension for YouTube Music playback data.
- Security token between extension and companion.
- Settings for token, server, presence and Discord client ID.
- Mini player for track, cover, progress and track link.
- Diagnostics window for server, extension, Discord, track and security status.
- Versioned release bundle with app and extension.

### Fixed

- More stable YouTube Music detection for track and video links.
- Queueing of extension states during WebSocket reconnects.
- Correct persistence and normalisation of the mini player window settings.
