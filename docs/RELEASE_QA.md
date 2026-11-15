# Release QA

Complete this checklist for each release tag before sharing the release widely.

## Before tagging

- `Directory.Build.props` and `YTMPresence/extension/manifest.json` specify the same version.
- `CHANGELOG.md` contains an entry for this version.
- `dotnet build YTMPresence.slnx --configuration Release` completes without errors.
- `dotnet test YTMPresence.Core.Tests/YTMPresence.Core.Tests.csproj` passes.
- `node --test` passes the extension behavior suite (Node.js 20 or later; no npm install required).
- `.\scripts\package-release.ps1` produces the app, bundle ZIP, extension ZIP (Chromium), extension ZIP (Firefox), Inno Setup EXE and then `SHA256SUMS.txt` (only once all artifacts are final).
- `.\scripts\verify-release.ps1 -RequireSetup` reports the expected version and runtime target and verifies `SHA256SUMS.txt` against the artifacts without rewriting the list.

## Release artifacts

- `YTMPresence-<version>-win-x64-setup.exe` starts without administrator rights.
- The setup shows the Inno Setup wizard and installs to `%LOCALAPPDATA%\Programs\YTMPresence`.
- `YTMPresence-<version>-win-x64.zip` contains `app/`, `extension/` and `RELEASE.txt`.
- `YTMPresence-extension.zip` contains the manifest, scripts, `i18n.js`, `_locales/`, options, popup and all icons.
- `YTMPresence-extension-firefox.zip` contains the same files plus `browser_specific_settings` in the manifest.
- `SHA256SUMS.txt` covers the bundle ZIP, both extension ZIPs and the setup EXE.

## Installation test

- Run the setup over an existing installation.
- YTMPresence starts after installation.
- The start menu shortcut launches the app.
- Windows lists YTMPresence among the installed apps.
- Uninstallation removes the start menu entry, the autostart value, the app folder and the Windows uninstall entry.

## Update test

- Install an older version.
- Run `Check for updates` from the tray menu. The menu stays open, the entry shows the running check, and the update entry appears below it without reopening the menu.
- Click the update notification while it is still on screen. The update installer window opens in the foreground; check the same after a restart with `CheckForUpdatesOnStartup` enabled. Note whether clicking the notification later in the Windows notification center does anything.
- The update menu entry opens the update installer window.
- `Download only` shows progress, target path and a confirmed SHA256 check, without exiting the app.
- `Install` launches the setup only after a successful SHA256 check; **YTMPresence then exits by itself** (the installer no longer terminates the app by process name).
- The setup window stays visible after YTMPresence has exited.
- **The app exits by itself within a few seconds, without a Restart Manager dialog.** This checks that `MakeInstanceKey` in the installer and `InstallerCoordination.GetInstanceKey` in the app produce the same key. The logic is implemented separately, and the unit test covers only the C# implementation. A Restart Manager dialog or shutdown timeout indicates a key mismatch, even if the update completes.
- Cancelling during the download removes the temporary files under `%TEMP%\YTMPresence\updates`.
- Closing the update window or shutting down the app aborts running downloads and attempts to remove their working directories.
- A tampered or missing `SHA256SUMS.txt` blocks the installation with an error message; the app stays usable.
- A portable YTMPresence copy running in parallel from another folder in a different user session is left untouched by the installation.
- After installation the app reports the new version.

## Local updater hotfix test

- Build a release candidate with `.\scripts\package-release.ps1`.
- Install an older published version.
- Run `.\scripts\test-update-flow.ps1 -SetupPath .\artifacts\release\YTMPresence-<version>-win-x64-setup.exe`.
- Complete the manual in-app update test described by the script.
- End the script with Enter and check that the original `settings.json` was restored.

## Silent installation test

- Only run this on a fresh Windows profile or in a VM.
- Run `.\artifacts\release\YTMPresence-<version>-win-x64-setup.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-`.
- Check that `%LOCALAPPDATA%\Programs\YTMPresence\app\YTMPresence.exe` exists.
- Run `%LOCALAPPDATA%\Programs\YTMPresence\unins000.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART`.
- Check that the app folder, start menu entry and Windows uninstall entry were removed.

## Clean test

- Install on a fresh Windows profile or in a VM.
- Use a machine without any .NET runtime or SDK (`dotnet --list-runtimes` is not found or lists nothing). The self-contained setup starts the app there without further downloads.
- `.\scripts\test-clean-install.ps1 -SetupPath <new setup> -PreviousSetupPath <previous setup>` tests installation, startup, update with settings and token preserved, and uninstallation in Windows Sandbox without .NET or network access. It requires the "Windows Sandbox" feature. Perform the remaining checks manually.
- The getting-started window appears on first start.
- The guide shows the companion version and four steps: install extension, copy token, save and check, open YouTube Music. The Chrome/Edge store link, Firefox instructions and separate developer section are accessible.
- The token and custom companion URL can be copied. “Later” closes without completing setup; “Done” can complete it manually. If copying, saving or opening a link fails, the message explains the next step in the selected language.
- The extension connects only after the saved token has been accepted on its background socket; merely opening a socket never gives a green badge.
- The Chromium extension (Chrome/Edge) and the Firefox extension (`about:debugging`) both connect.
- With the browser and Windows set to English, the extension popup, options and tray windows appear in English. With both set to German, they appear in German.
- The app starts reliably without Discord running; diagnostics report Discord as unreachable and connect automatically once Discord is running.
- The diagnostics window shows the status of the server, extension and Discord.
- The diagnostics report ZIP can be created and contains no raw `SecurityToken` line.

## Guided setup acceptance

See [2026-10-12 setup QA](SETUP_QA_2026-10-12.md) for the tested implementation and results. Record automated tests, direct observations and user-assisted checks separately; leave unperformed checks open.

- Use a fresh local Edge extension or dedicated test profile with only one YTMPresence installation enabled. Confirm “Local/unpacked” and the loaded source directory; an unreleased build can carry the same manifest version as the published one.
- With no token, both popup and options direct the user to copy it. Leave the default URL unchanged, and verify that custom URL controls are under Advanced settings.
- Enter a wrong token and choose **Save and check**. Expect **Saved – token rejected**, a non-green badge and instructions to copy the current token. Then use the real token without pasting it into the QA report.
- With no received track, confirm the companion connection and the instruction to open/reload YouTube Music. Do not accept this as evidence of visible Discord presence.
- Edit the options without saving. The unsaved notice must explain that status describes the saved connection. Popup **Test connection** must still check saved settings.
- After saving the correct token, reload an existing YouTube Music tab and start a track. Confirm that popup, mini player and visible Discord presence agree.
- Stop the companion: popup/options must leave the connected state and provide a next action. Restart it without reloading YouTube Music; the selected playing track must return automatically. Repeat with a paused track and verify the existing pause/idle-clear policy still applies.
- Check the extension version and installation type in the popup and options, and the companion version in the setup guide. An older companion without the optional version field must remain connected and show **Not reported**. A packaged install must be called **Package installation**, without inferring a store origin.
- Repeat the workflow in German and English. Navigate options using Tab/Shift+Tab and Enter, expand Advanced settings with the keyboard, and check visible focus. In the setup guide, check scrolling at reduced window height, button focus, Later and manual Done. Use a suitable test profile or environment; do not change the user's system language for this check.
- Record storage failure, unreachable companion, timeout, token switch during checking and late-socket responses in automated regressions. Keep browser/OS live behavior separate from mock results.

## Multiple tabs in one browser profile

Run these checks in Chrome, Edge and the packaged Firefox variant, recording versions and results. Automated fixtures alone cannot confirm compatibility with live YouTube Music or Discord.

In the [2026-10-12 Edge / Discord QA](LIVE_QA_2026-10-12.md), the corrected local extension passed the Edge retest for single-tab commands, source switching, pause clearing, autoplay timing, reload, navigation and closure. The report distinguishes the initial Web Store run from the retest and lists untested scenarios and browsers. It is not a complete release sign-off.

For unpacked-extension testing, confirm the loaded directory is the repository's `YTMPresence/extension`, reload the extension after source changes, and reload the test tabs so their page/content scripts also update. Keep only one YTMPresence extension active in the test profile. The manifest version alone cannot distinguish an unreleased checkout from the published extension.

- Open two YouTube Music tabs with distinct tracks. Play in A and keep B paused. The extension popup, mini player and Discord continue to show A when B reports state or gains focus.
- Use play/pause, next and previous in the mini player. Only A reacts.
- Pause A while B remains paused. Play resumes A. Start B while A is paused; the displayed track and commands switch to B.
- Where the browser permits simultaneous playback, keep both tabs playing. The selection stays stable; pausing the selected tab selects the other playing tab.
- Let the selected tab advance through its autoplay queue, and compare its elapsed/total time with the mini player and Discord. YouTube Music can keep a cumulative media-element timeline; the reported progress must restart relative to the new track. Seek within that track and confirm the displayed progress follows.
- Close, reload, navigate away from and discard the selected tab in separate checks. A valid fallback is selected; commands are not sent to a removed document. A normal in-page YouTube Music track change still updates the selected source.
- Restart the extension background worker with both tabs paused. After state recovery, Play targets exactly one tab and the popup identifies that tab's track.
- Close all YouTube Music tabs. The popup clears the track, commands affect no tab, and Discord clears after the configured idle timeout. Changing the token must not replay the closed track.
- These checks cover one browser profile. Since 1.6.0 the companion also selects one source across browsers and profiles; the [2026-11-12 live QA](LIVE_QA_2026-11-12.md) records the Chrome and Edge check.

## Discord album art

Unit tests cover which URL the companion sends, not how Discord renders it. Check the visible activity in the Discord desktop app, ideally also from a second account.

- Play an album track and a music video. The activity's large image shows each track's cover without a play/pause icon on top; the album hover text remains. For the music video, the image shows the video thumbnail without black bars at the top and bottom. Note how Discord fits the 16:9 image into its square frame.
- Change tracks and confirm the cover follows within one update interval. Pause and resume; the cover stays and the state line shows the paused state.
- Turn off **Show album art in Discord** and save. The YTM Presence logo with its play/pause icon returns within one update interval; turning the option on restores the cover.
- During an advertisement with **Show as ad**, the logo appears instead of a cover.
- If Discord rejects the activity, diagnostics show Discord's reason. Record the cover URL's host and length, not the full URL, when reporting it.

## Discord status line

Check from a second account or another device, since Discord shows your own status differently.

- While a track plays, the member list shows "Listening to <song title>". Change tracks; the status follows.
- Turn off **Show the song title in Discord's status line** and save. With the next update the member list shows the Discord application's name again ("Listening to <application name>").
- During an advertisement with **Show as ad**, the status shows the application name.
- The profile card and "Active Now" keep showing the application name in their header; only the status line changes.
- Confirm that Discord accepts the activity: diagnostics show the last result as accepted, not rejected.

## Discord title link

- From a second account, open the activity on the profile card and click the song title. YouTube Music opens the playing track.
- Play a track from a playlist or a shared link with extra parameters; the title still opens that track, and diagnostics show the activity as accepted.
- During an advertisement with **Show as ad**, the title is not a link.

## After pushing

- The GitHub Actions release workflow passes.
- The GitHub release is not marked as draft or prerelease.
- The release notes match the changelog entry.
- The assets are present in the release and downloadable.
