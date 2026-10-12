# Release QA

This checklist is meant for every release tag, before the release is shared widely.

## Before tagging

- `Directory.Build.props` and `YTMPresence/extension/manifest.json` carry the same version.
- `CHANGELOG.md` contains an entry for this version.
- `dotnet build YTMPresence.slnx --configuration Release` completes without errors.
- `dotnet test YTMPresence.Core.Tests/YTMPresence.Core.Tests.csproj` is green.
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
- Run `Check for updates` from the tray menu.
- The update menu entry opens the update installer window.
- `Download only` shows progress, target path and a confirmed SHA256 check, without exiting the app.
- `Install` launches the setup only after a successful SHA256 check; **YTMPresence then exits by itself** (the installer no longer terminates the app by process name).
- The setup window stays visible after YTMPresence has exited.
- **The app exits by itself within a few seconds, rather than through the Restart Manager dialog.** This is the only visible evidence that `MakeInstanceKey` in the installer and `InstallerCoordination.GetInstanceKey` in the app compute the same key. Both sides are second implementations of the same logic; the unit test only pins the C# side. If the Restart Manager dialog appears instead, or shutting down takes until the timeout, the keys have drifted apart — the update still looks successful.
- Cancelling during the download removes the temporary files under `%TEMP%\YTMPresence\updates`.
- Closing the update window and shutting down the app abort running downloads and remove the respective working directory as best they can.
- A tampered or missing `SHA256SUMS.txt` blocks the installation with an error message; the app stays usable.
- A portable YTMPresence copy running in parallel from another folder in a different user session is left untouched by the installation.
- After installation the app reports the new version.

## Local updater hotfix test

- Build a release candidate with `.\scripts\package-release.ps1`.
- Install an older published version.
- Run `.\scripts\test-update-flow.ps1 -SetupPath .\artifacts\release\YTMPresence-<version>-win-x64-setup.exe`.
- Carry out the manual in-app update test it describes, in full.
- End the script with Enter and check that the original `settings.json` was restored.

## Silent installation test

- Only run this on a fresh Windows profile or in a VM.
- Run `.\artifacts\release\YTMPresence-<version>-win-x64-setup.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-`.
- Check that `%LOCALAPPDATA%\Programs\YTMPresence\app\YTMPresence.exe` exists.
- Run `%LOCALAPPDATA%\Programs\YTMPresence\unins000.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART`.
- Check that the app folder, start menu entry and Windows uninstall entry were removed.

## Clean test

- Install on a fresh Windows profile or in a VM.
- The getting-started window appears on first start.
- Token and companion URL can be copied.
- The extension connects once token and URL are saved.
- The Chromium extension (Chrome/Edge) and the Firefox extension (`about:debugging`) both connect.
- With an English browser and Windows language, the extension popup, the options and the tray windows appear in English; with German, in German.
- The app starts reliably without Discord running; diagnostics report Discord as unreachable and connect automatically once Discord is running.
- The diagnostics window shows server, extension and Discord sensibly.
- The diagnostics report ZIP can be created and contains no raw `SecurityToken` line.

## Multiple tabs in one browser profile

Run this smoke check in Chrome, Edge and the packaged Firefox variant; record the tested versions and results. The automated fixtures do not establish live YouTube Music or Discord compatibility.

- Open two YouTube Music tabs with distinct tracks. Play in A and keep B paused. The extension popup, mini player and Discord continue to show A when B reports state or gains focus.
- Use play/pause, next and previous in the mini player. Only A reacts.
- Pause A while B remains paused. Play resumes A. Start B while A is paused; the displayed track and commands switch to B.
- Where the browser permits simultaneous playback, keep both tabs playing. The selection stays stable; pausing the selected tab selects the other playing tab.
- Close, reload, navigate away from and discard the selected tab in separate checks. A valid fallback is selected; commands are not sent to a removed document. A normal in-page YouTube Music track change still updates the selected source.
- Restart the extension background worker with both tabs paused. After state recovery, Play targets exactly one tab and the popup identifies that tab's track.
- Close all YouTube Music tabs. The popup clears the track, commands affect no tab, and Discord clears after the configured idle timeout. Changing the token must not replay the closed track.
- This slice covers one browser profile. Coordination between separate browser/extension connections remains an open roadmap item.

## After pushing

- The GitHub Actions release workflow is green.
- The GitHub release is not marked as draft or prerelease.
- The release notes match the changelog entry.
- The assets are present in the release and downloadable.
