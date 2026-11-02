# Changelog

All notable changes to YTMPresence are documented in this file.

## [Unreleased]

### Fixed

- With several browsers or browser profiles connected at once, one of them decides the presence, the mini player and the target of player commands. A paused browser no longer overwrites the presence of a playing one, and a click in the mini player affects one browser instead of all of them. The rule matches the one within a browser: a playing source keeps the selection; when it pauses, another one that is playing takes over; when the selected browser disconnects, the remaining one takes over at once. The extension protocol is unchanged, so older extensions benefit as well.

### Changed

- The diagnostics window and the diagnostics report name the selected source, for example "Edge 155 #3".
- When Discord closes the connection, the diagnostics show Discord's reason, for example "Invalid Client ID (code 4000)", instead of only saying that the connection closed.

## [1.5.1] - 2026-11-02

### Fixed

- The first presence after connecting to Discord is no longer lost. Discord silently drops commands that arrive before its READY message, and the companion sent the activity right after the handshake. After every start of the companion or of Discord the first update vanished; with a paused track, whose details do not change, the presence could stay missing for a long time. The companion now waits for READY before it sends anything.

### Changed

- The companion waits for Discord's answer to every activity update and reports it: accepted, rejected with Discord's code and reason, or no answer within three seconds. Until now an update counted as successful once it was written; a rejection only appeared in the log while the diagnostics showed OK. The tray menu, the diagnostics window and the diagnostics report show the result.

### Notes

- The browser extension is unchanged apart from its version number, which follows the companion's. The Chrome Web Store stays at 1.5.0.

## [1.5.0] - 2026-11-02

### Added

- A four-step companion setup guide with Chrome/Edge store and Firefox links, a separate developer section, manual token copying and localized German/English feedback. Companion version is shown in setup; extension version and installation type are shown in popup/options without additional permissions.
- Optional `companionVersion` in `YTM_TEST_RESULT`. Older peers remain compatible; missing versions show “Not reported”, and mismatched versions do not block the connection.
- An extension behavior suite using Node's built-in test runner, executed in CI and the release workflow. It covers tab selection, targeted commands, tab lifecycle, background restart, pending state, playback extraction and track URLs without contacting YouTube Music or Discord.
- `scripts/test-clean-install.ps1` tests install, start, update and uninstall in Windows Sandbox, which has no .NET and no network.

### Changed

- The setup and the ZIP are self-contained and include .NET. Until now they needed both the .NET 10 Desktop Runtime and the ASP.NET Core Runtime, and neither the setup nor the README said so; without the ASP.NET Core Runtime the companion did not start. The setup grows from 2.2 MB to about 62 MB.
- Extension options now use **Save and check**. Saving and connection changes are coordinated in the background, while popup retries use the saved configuration on the same connection. Custom URLs are under Advanced settings. Feedback separates storage errors, saved-but-unreachable and saved-but-rejected results, and explains separate browser-profile/install settings.
- The Firefox package requires Firefox 140 or later instead of 126. It declares its data use with `data_collection_permissions`, which older versions do not know.
- The release workflow verifies the extension archives and lints the Firefox package before publishing, as CI already did. Both workflows run on Node 24.

### Fixed

- Within one browser profile, a playing YouTube Music tab retains ownership of the presence. Paused background tabs no longer overwrite it, and mini-player commands go only to the selected tab's main frame. Pausing retains the selected tab unless another tab is playing; closing, reloading, navigating away or discarding it chooses a fallback in stable tab-ID order, preferring playing tabs.
- Autoplay progress uses the current track's player-bar timeline when available. YouTube Music can reuse a cumulative media timeline across tracks, which previously made the mini player and Discord show the previous track's elapsed time as part of the new track. Invalid or hidden player-bar values retain the media-element fallback.
- A green extension badge now requires a positive token response on the current socket, rather than an open socket alone. Four-second checks and bounded reconnect delays ignore obsolete sockets/configurations, and reconnect replays the current selected track after authentication. Keepalive replies do not replay playback or refresh pause/idle state. Popup and options share actionable states and distinguish companion connectivity from visible Discord presence.
- Background-worker restarts recover playback state from open tabs, including paused tracks. Closed tabs cannot be restored by late recovery responses or replayed after a token change, and obsolete WebSocket connections cannot deliver player commands.
- Update errors no longer repeat their prefix. The tray showed messages like „Update check failed: Update-Check fehlgeschlagen: HTTP 404.“, because the message from the core carried its own German prefix. The messages of the update check, the download and the setup start are now English, like the log; the text around them still follows the Windows language. The first log line is English as well.
- The settings, diagnostics and mini-player windows follow the Windows language completely. Their status and validation messages were hard-coded German („Token kopiert.“, „Host darf nicht leer sein.“, „Keine Extension verbunden“), and the diagnostics window mixed German title, buttons and labels with English ones in either language. The diagnostics report itself stays English, like the log; its two German lines are English now. The Discord connection errors shown in diagnostics are English as well.

### Known limitations

- With several browsers or browser profiles connected at the same time, the companion still sends mini-player commands to all of them. Selecting one source across connections is planned for 1.6.0. The companion protocol and the idle-clear behavior are unchanged.

## [1.4.0] - 2026-08-09

### Changed

- The project documentation is now in English: the changelog including its history, the README and everything under `docs/`. The extension is published in an international store, and German documentation no longer matched that.

### Fixed

- **The Discord presence is no longer hard-coded to German.** Playback state, the paused line, the advertisement notice and the label of the track button were fixed strings — an English user was showing their contacts „Pausiert bei 1:23" and a button called „Auf YouTube Music öffnen". This is the text *other people* read, not the app's own interface. `PresenceText` in the core project now translates according to the system language, falling back to English instead of German.
- The diagnostics window and the getting-started window follow the system language as well. Server state, the connection and Discord lines, the age labels, the update-check line and the error message when opening YouTube Music were all hard-coded German. The visible interface is now fully translated.
- Log messages are English. They were German, which was the least useful of the options: understandable to the maintainer, opaque to everyone else, and no longer matching the now-English documentation. Logs are deliberately **not** translatable — a log line is a search key, and a localised message no longer matches the string in the source when a user reports it. The diagnostics window keeps following the system language; that is the surface built for users, whereas the log is the surface built for diagnosis.
- The mini player follows the Windows language. Playback state, the no-track placeholder and the label of the previous-track command were hard-coded German and stayed German even when the rest of the interface ran in English.

## [1.3.1] - 2026-08-09

### Changed

- **The extension no longer requests the `tabs` permission.** It was never needed: `tabs.query` with a URL filter and `tabs.sendMessage` both work through the host permission for `https://music.youtube.com/*`, `tabs.create` needs no permission at all, and only the tab ID is read from the results. In exchange, Chrome displayed "Read your browsing history" at install time — for a capability the extension never used.
- The short description and store text now mention the companion app first. Without it the extension does nothing, because Discord Rich Presence cannot be set from a browser. The short description comes from `_locales/<language>/messages.json` and is taken by Chrome from the manifest, not from the store dashboard.
- `docs/STORE_SUBMISSION_CHECKLIST.md` now contains the data-collection answers for the Chrome dashboard, aligned with the Firefox metadata already declared.

## [1.3.0] - 2026-08-09

### Fixed

- **The presence can be cleared again.** To clear it, the Discord IPC client sent the command `CLEAR_ACTIVITY` — which does not exist in the RPC protocol; the documented commands are `SET_ACTIVITY`, `SEND_ACTIVITY_JOIN_INVITE` and `CLOSE_ACTIVITY_REQUEST`. Discord rejected the command, the rejection was discarded, and the app kept reporting "Discord OK". As a result the presence stayed up: `OnlyShowWhenPlaying` had no effect, `PausedClearSeconds` had no effect, and it did not disappear on shutdown either. Clearing now uses `SET_ACTIVITY` with `activity: null`.
- **Track URLs no longer lose their percent encoding.** Normalisation used `Uri.ToString()` in three places — per the documentation the *unescaped* canonical form ("All characters are unescaped except #, ? and %"). `?q=Sam%20Smith` therefore became `?q=Sam Smith` with real spaces. Discord then rejected the entire activity (`"url" must be a valid uri`) and no presence appeared at all. The search links used as a fallback when no video ID can be found were affected. `AbsoluteUri` is used instead.
- An invalid button URL no longer costs the whole presence. The URL is validated before the button is built; if it does not hold up, only that button is dropped and a log line states why.
- A presence update is no longer preceded by a clear. The former "Discord cache fix" sent a clear on every track and play/pause change and waited 200 ms. That was inconsequential while clearing used the non-existent `CLEAR_ACTIVITY` command — with clearing repaired, the same sequence would have made the presence vanish briefly on every track change. `SET_ACTIVITY` replaces the activity on its own anyway.
- Error responses from Discord are logged instead of discarded. That gap is exactly what hid the wrong command for the entire lifetime of the project.

### Added

- The diagnostics window shows the last successful update check. Failures of the automatic check at startup deliberately go to the log only, so that no balloon appears every time the machine boots without a network — but that also meant the check could fail for months without anyone noticing. If the last success is more than two weeks ago, the line says so explicitly; if the check at startup is disabled, that is stated too. The timestamp is stored in the settings and included in the diagnostics report.

## [1.2.0] - 2026-08-05

### Added

- `scripts/verify-extension-archives.ps1` inspects the built extension archives the way a store upload pipeline sees them: entry names (path separators, absolute paths, traversal), a real extraction to disk, and resolution of every `__MSG_*__` placeholder against the default-language catalogue.
- CI now builds the release artifacts itself (`package-release.ps1 -SkipInstaller`), verifies them, and lints the **extracted** Firefox variant from the built archive rather than the source folder. Linting the source misses packaging defects because it never touches the archives.

### Changed

- **The companion WebSocket now checks the `Origin` of the handshake.** WebSocket connections are not subject to the same-origin policy: until now any website you visited could talk to the local companion and guess tokens through `YTM_TEST` without limit. In practice the 256-bit token protected against that, but it remained a fingerprinting surface and an open endpoint. Extension origins (`chrome-extension:`, `moz-extension:`, `safari-web-extension:`) and a missing origin for local non-browser clients are allowed; web origins are rejected with HTTP 403.
- Sockets that do not present a valid token within 60 seconds are closed. A valid token in the connection test counts as proof, because that is exactly how the extension keeps the connection alive while playback is paused.
- The duplicated implementation of the instance-key calculation (`InstallerCoordination.GetInstanceKey` and `MakeInstanceKey` in `installer/YTMPresence.iss`) is marked as such on both sides, with a shared test vector. `docs/RELEASE_QA.md` now checks the self-shutdown as an item of its own: it is the only visible evidence that both sides compute the same key — if they drift apart, the installer silently falls back to the Restart Manager and the update still looks successful.
- The setup asset of a release is selected exactly once; name and download URL then come from the same asset. Multiple assets ending in `-setup.exe` are rejected as ambiguous, just like conflicting checksum lines, instead of silently taking the first — the order of the GitHub response is not guaranteed.
- Releases marked `prerelease: true` are no longer offered as updates. `/releases/latest` already filters them, but `UpdateApiUrl` is configurable.
- Selection and verification of the checksum asset now both compare the file name ordinally. A differently cased `sha256sums.txt` is therefore never offered in the first place, instead of failing later with a misleading security message. The artifact names live in `UpdateArtifactNames` as a single source.

### Fixed

- **Paused playback is recognised as paused again.** The extension read the playback state preferentially from `navigator.mediaSession.playbackState` and only fell back to the media element when that was empty. Since YouTube Music does not update that value reliably on pause, the extension kept reporting "playing" — the presence stayed up in Discord, and neither `OnlyShowWhenPlaying` nor `PausedClearSeconds` could take effect, because the companion never saw a pause. The media element's `paused` is now authoritative; `mediaSession` only serves as a fallback. Seeks (`seeking`) are skipped so that the presence does not flicker while scrubbing.
- The search for the media element now considers `<video>` as well. If YouTube Music offered no `<audio>`, position, duration and playback state previously had no source at all, and the state silently fell back to "playing".
- **The presence no longer links an arbitrary video from the page.** `getPlayerBarWatchUrl` contained selectors without a container scope (`a[href*="/watch?v="]` and `a[href*="music.youtube.com/watch?v="]`) which grab the first matching link in the entire document — on YouTube Music that means queue entries, recommendation tiles or the sidebar. Since that source also took precedence, a track that had never played was linked. Only anchors inside `<ytmusic-player-bar>` count now, and the artwork comes first: the player sets it per track and it is the only source that reliably refers to the current title.
- The track URL now considers the address bar as well. As of 2026-08, YouTube Music provides neither the player-bar anchors nor `canonical`, and `og:url` points at `/app_shell` without a video ID — leaving the artwork, the last fallback, as the only working source. The candidates are reordered by reliability: player bar, artwork, address bar, canonical. The previously identical branches for `video` and default mode were merged.
- Release ZIPs use forward slashes as path separators. `Compress-Archive` on Windows PowerShell 5.1 writes backslashes, which violates the ZIP specification: spec-compliant extractors — including the upload pipelines of the Chrome Web Store and addons.mozilla.org — read `_locales\en\messages.json` as a flat file name rather than a directory, so the `__MSG_*__` placeholders in the manifest fail to resolve. Both extension ZIPs and the bundle ZIP were affected.
- `verify-release.ps1` no longer normalises backslash entries to forward slashes but aborts naming the offending entries. The previous normalisation reported exactly the defect it was meant to find as being in order.

## [1.1.0] - 2026-08-05

### Added

- Version consistency check across app, assembly metadata, extension and release changelog; CI and release packaging abort on any mismatch.
- Settings recovery preserves corrupt JSON files as `settings.corrupt-<timestamp>.json` and then creates valid defaults.
- Update security components in `YTMPresence.Core.Updates` (URL validation, checksum parser, size limits, temporary working directories, download service, launch verification) with extensive tests for checksum, download, redirect and process-start cases.
- Settings flag `AllowLoopbackUpdateSource` (default: off) as an explicit test configuration for the local QA update server; `scripts/test-update-flow.ps1` sets it automatically and restores the original settings afterwards.

### Changed

- Settings are written through a temporary file and an atomic file replace.
- Firefox packages declare the local transfer of browsing activity and website content through Mozilla's consent metadata, without raising the existing minimum version of 126.
- **Updates only run after a successful SHA256 verification.** A missing `SHA256SUMS.txt`, missing, duplicate or conflicting setup lines, and invalid or mismatching hashes all block the installation; the previous "without checksum comparison" path is gone.
- Update sources are restricted to HTTPS on `api.github.com`, `github.com`, `release-assets.githubusercontent.com` and `objects.githubusercontent.com`; metadata and asset redirects are validated individually (max. 5, with loop detection), downloads have size limits (setup 300 MB, checksums 64 KB, metadata 4 MB) as well as connect, stall and overall timeouts. Custom `UpdateApiUrl` values outside this policy are rejected with a clear error message.
- Every update attempt uses a unique temporary directory with a `.part` file and an atomic rename after verification; errors and cancellations clean up, and orphaned directories are removed by age at app start.
- Immediately before the setup is launched, existence, name, size and SHA256 are checked again and the file is locked against modification during the launch; YTMPresence only exits once the setup has actually started and stays usable if the launch fails.
- The Inno Setup installer no longer terminates processes by file name via `taskkill`: it asks the installed instance to shut down cleanly through a shutdown event bound to the target path with a collision-resistant SHA256, and otherwise uses the Windows Restart Manager (`CloseApplications=force`), which only closes processes holding files in the installation folder. Portable copies and unrelated processes with the same name are left alone.
- `verify-release.ps1` only verifies `SHA256SUMS.txt` (existence, exactly one valid line per artifact, correct hashes, no stale, duplicate or unknown lines) instead of rewriting it; the list is produced solely in `package-release.ps1`, once all artifacts are final. The release workflow builds and tests the solution before packaging.

### Fixed

- WebSocket text messages stay intact when a UTF-8 character is fragmented across several frames; binary and oversized messages are still discarded.
- Server start, stop and presence updates are serialised; idle tasks and Discord resources are fully torn down on restarts.
- A backwards-compatible keepalive over the existing connection test keeps the Manifest V3 WebSocket connection alive while playback is paused, without resetting the pause or idle timeouts.
- The final release checksum list now also contains the Firefox ZIP and — if built — the setup EXE.

## [1.0.0] - 2026-06-20

### Added

- Test project `YTMPresence.Core.Tests` (xUnit) with tests for the security token, settings normalisation, update check (SemVer/asset selection), presence logic and CompanionServer token authentication.
- PR CI workflow (`.github/workflows/ci.yml`): build, tests and `web-ext lint` on pull requests and pushes to `main`.
- English localisation of the browser extension through `chrome.i18n` (`_locales/en` as default, `_locales/de`); popup, options and manifest are now DE/EN.
- Firefox support: the release pipeline additionally produces `YTMPresence-extension-firefox.zip` with `browser_specific_settings`.

### Changed

- The tray windows (getting started, settings, mini player, update) now consistently use the central `UiText` strings and are therefore DE/EN.
- When Discord is not running, the Discord IPC client no longer tries every pipe on every message but enters a short cooldown and reconnects automatically.
- Release verification checks the new extension files (`i18n.js`, `_locales`) and both extension ZIPs (Chromium + Firefox).

### Removed

- Dead legacy project `YTMPresence.Tray` (not part of the solution) removed.

## [0.8.11] - 2026-06-01

### Fixed

- The Inno Setup update now only terminates YTMPresence itself and no longer the setup launched from it.

## [0.8.10] - 2026-05-15

### Changed

- The release script builds the installer solely through Inno Setup, which makes it easier to maintain.

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

- The release script prefers Inno Setup and falls back to the previous legacy installer locally when `ISCC.exe` is missing.

## [0.8.8] - 2026-05-15

### Added

- The diagnostics window can now produce a diagnostics report as a ZIP containing status, environment, redacted settings and logs.

## [0.8.7] - 2026-05-15

### Fixed

- The update installer now launches the setup with an explicit working directory and no longer exits the app before the installer is visibly running.

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

- The installer now waits until the setup EXE is readable after IExpress has produced it, before SHA256 checksums are computed.
- The mini player tooltip uses the correct umlaut in `Track öffnen` again.

## [0.8.3] - 2026-05-15

### Added

- The Windows installer now registers YTMPresence in the Windows app settings with version, icon, size and uninstall command.

### Changed

- Installer and release script check cleanup targets more carefully before removing old artifacts.
- On update, the installer only terminates the installed YTMPresence instance from the installation folder.

### Fixed

- Installation aborts earlier with a clear error message when `YTMPresence.exe` is missing after extraction.
- Uninstallation removes the Windows uninstall entry and cleans up the temporary cleanup script itself.

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
- Release packaging checks after the build that app, extension and ZIP contents are complete.

### Fixed

- The release bundle now copies the app files into the `app` folder correctly.
- Incomplete releases are prevented by an automatic ZIP structure check.

## [0.7.0] - 2026-05-14

### Added

- Extension popup with companion status, token status, URL and last track.
- Badge on the extension icon for connection status.
- Connection test directly in the popup.
- Faster state updates on play/pause, seek and YouTube Music navigation.

### Changed

- More robust injection guards for the content and page script.
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
