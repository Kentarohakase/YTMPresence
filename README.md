# YTMPresence

YTMPresence shows the current YouTube Music track as Discord Rich Presence. The project consists of two parts:

- a local companion app in the tray that talks to Discord
- a browser extension that sends the current YouTube Music state to the companion

**The extension is available in the Chrome Web Store:**
[YTM Discord Presence Bridge](https://chromewebstore.google.com/detail/ytm-discord-presence-brid/obdinnbalnbmpkcaopnhnkdgdkcgligi)

It only works together with the companion app for Windows from the [releases](https://github.com/Kentarohakase/YTMPresence/releases) — Discord Rich Presence cannot be set from a browser.

## Installation

### Requirements

- Windows (x64) with the Discord desktop app.
- Chrome or Edge 116 or later, or Firefox 140 or later.
- From 1.5.0 on, the setup and the ZIP include .NET; nothing else has to be installed.
- 1.4.0 and older do not bring .NET with them. Install both of these from the [.NET 10 download page](https://dotnet.microsoft.com/download/dotnet/10.0) first, each as x64:
  - **.NET Desktop Runtime 10**
  - **ASP.NET Core Runtime 10**

  The Desktop Runtime alone is not enough; without the ASP.NET Core Runtime the companion does not start.

### 1. Start the companion

Run `YTMPresence.exe`. The tray icon then appears in the Windows notification area.

On first start the app generates a security token automatically and stores the settings at:

```text
%APPDATA%\YTMPresence\settings.json
```

### 2. Follow the four setup steps

Open **Getting started** from the companion tray menu whenever you need the setup guide. It shows the companion version and these steps:

1. **Install the extension.** For Chrome/Edge, use [YTM Discord Presence Bridge in the Chrome Web Store](https://chromewebstore.google.com/detail/ytm-discord-presence-brid/obdinnbalnbmpkcaopnhnkdgdkcgligi). Firefox uses the [instructions below](#firefox-desktop).
2. **Copy the token** in the guide or through the tray menu.
3. **Save and check in the extension options.** Paste the token, then choose **Save and check**. The extension saves the settings first and waits for the companion to accept the token on its active connection. The normal endpoint is `ws://127.0.0.1:17373/ws` (with `localhost` as a fallback when no custom URL is saved). A different local URL can be entered under **Advanced settings**; use the companion's displayed URL if its address was customized.
4. **Open YouTube Music.** After switching extensions, reload existing [YouTube Music tabs](https://music.youtube.com/), then start a track and check the visible presence in Discord.

**Each browser profile and each store/local installation has its own settings.** Copy the token into the options of the installation you actually use. Enable only one YTMPresence extension per profile. A version number alone cannot distinguish an unreleased checkout from the published extension.

Popup and options show the extension version, installation type and, when reported, companion version. “Package installation” does not necessarily mean “installed from a store”; “Local/unpacked”, “Managed” and “Unknown” are the other labels. A missing companion version is “Not reported”, and different versions do not by themselves prevent connecting.

| Status or result | Next action |
| --- | --- |
| Token missing | Copy the current token from the companion; paste it in the extension options and save and check. |
| Connecting / Checking token | Keep the companion running and wait for the four-second check. Opening a socket alone does not produce a green badge. |
| Saved – token rejected | The settings were saved, but authentication failed. Copy the current companion token and save and check again. |
| Saved – companion not reachable / Connection interrupted | Start the companion and retry. If needed, check the advanced URL. Network failures reconnect automatically with delays from 500 ms to 10 seconds. |
| Not saved | Reopen options and retry saving. The previously saved connection has not been replaced by the failed write. |
| Companion connected, no track data | Open YouTube Music or reload its existing tab and start a track. |

Unsaved inputs are labelled explicitly; the status always describes the saved background configuration. **Test connection** in the popup retries that saved configuration. A confirmed companion connection is **not** confirmation that Discord accepted or displays a presence: check Discord itself.

The interfaces follow the browser/Windows language; German and English are supported. Setup can be left with **Later** or completed manually with **Done**.

#### Firefox Desktop

For Firefox Desktop 140 and later, use the Firefox archive from the release:

1. Extract `YTMPresence-extension-firefox.zip`.
2. Open `about:debugging#/runtime/this-firefox`.
3. Choose **Load Temporary Add-on…** and select the extracted `manifest.json`.
4. Continue with token copying and **Save and check** above.

#### Developer setup: unpacked Chrome/Edge extension

1. Open `chrome://extensions` or `edge://extensions` and enable developer mode.
2. Choose **Load unpacked** and select the repository's `YTMPresence/extension` folder (or the extracted Chromium release extension).
3. Disable other YTMPresence installations in this profile and configure this installation's token.
4. After source changes, reload the extension, then reload existing YouTube Music tabs. Options should show **Local/unpacked**.

### Multiple YouTube Music tabs

Within one browser profile, the extension selects one tab for both the displayed track and mini-player commands. A playing tab keeps the selection while it plays; paused background tabs cannot replace it. If the selected tab pauses, another playing tab takes over. If none plays, the selection stays on the paused tab so that Play resumes it.

Closing, reloading, navigating away from or discarding the selected tab selects an available fallback. After a background-worker restart, the extension retrieves the latest state from open tabs, preferring a playing tab. When no source remains, the popup clears its track and the companion clears Discord presence through its existing idle timeout (120 seconds by default).

### Several browsers or profiles

The companion applies the same rule across browsers and browser profiles: a playing browser keeps the selection, another playing one takes over when it pauses, and the remaining one takes over at once when the selected browser closes. Only the selected browser receives mini-player commands. The diagnostics window shows which one is selected, for example "Edge 155 #3".

## Changelog

All release changes are listed in [CHANGELOG.md](CHANGELOG.md).

Planned improvements, priorities and completion criteria are tracked in [ROADMAP.md](ROADMAP.md).

## Publishing the extension

Prepared store and privacy texts are available at:

- [Chrome Web Store](docs/CHROME_WEB_STORE.md)
- [Extension privacy notice](docs/EXTENSION_PRIVACY.md)
- [Store submission checklist](docs/STORE_SUBMISSION_CHECKLIST.md)

## Updates

The tray app can check the latest GitHub release through `Check for updates` in the tray menu. If the release contains a setup EXE, the app can download and verify the update and launch the setup directly. It checks once at startup as well, provided `CheckForUpdatesOnStartup` is enabled in the settings.

The update download is protected:

- **Mandatory SHA256 verification**: a setup is only launched if the release contains a `SHA256SUMS.txt`, that file holds exactly one precisely matching line for the setup file, and the computed hash matches exactly. If the checksum file or the line is missing, or the hash is invalid or contradictory, the update is not carried out.
- **Permitted update sources**: real updates are only fetched over HTTPS from `api.github.com`, `github.com`, `release-assets.githubusercontent.com` and the historical asset host `objects.githubusercontent.com`. The initial URL, every redirect and the final target are validated individually; HTTP, direct IP addresses, loopback, private and link-local targets as well as foreign hosts are rejected. Normal TLS certificate validation stays active; a custom DNS pinning implementation is deliberately not provided. Only for the local QA test (`scripts/test-update-flow.ps1`) can an HTTP loopback source be permitted through the explicit settings flag `AllowLoopbackUpdateSource`.
- **Size limits and timeouts**: setup (300 MB), checksum file (64 KB) and release metadata (4 MB) have hard upper bounds, enforced before the download (Content-Length) and while streaming. Connection setup, stalled data and total duration are covered by timeouts; cancelling through the window or on app shutdown cleans up properly.
- **Temporary files**: every update attempt uses its own unique directory under `%TEMP%\YTMPresence\updates`. The download goes into a `.part` file and is only moved to its final name after a successful hash check; a `.part` file is never launched. Errors, user cancellation, closing the window and app shutdown abort ongoing I/O and clean up as best they can. If a setup that has already started still holds its file locked, the orphaned directory is removed by age at the next app start.
- **Verification immediately before launch**: right before the process starts, existence, file name, size and SHA256 are checked again; the file stays locked against modification during the launch. The app only exits once the setup has actually started — if the launch fails, YTMPresence remains usable.

**Limits of SHA256 verification**: the `SHA256SUMS.txt` comes from the same GitHub release as the setup. It protects against transfer errors and accidental tampering with individual files, but it does not establish cryptographic authenticity for the release as a whole: anyone able to replace both the setup **and** the checksum file in the release (for example with a compromised release account) would not be detected. It is not a substitute for a digital signature or a publisher attestation.

**Stopping the running app during installation**: the installer does not terminate processes by file name. It first asks the instance running from the target directory to shut down cleanly through a path-bound local mechanism. If that instance does not respond, the Windows Restart Manager only closes processes that actually hold files open in the installation folder. A portable copy running in parallel from a different folder is left untouched.

## Tray menu

- `Settings...`: opens the settings window for token, server, presence and Discord client ID
- `Open mini player`: shows the current track, cover, progress, play/pause, previous/next and a button to open the track
- `Open diagnostics`: shows server, extension, Discord, track and security status, and can produce a diagnostics report without the security token
- `Only show presence while music plays`: clears the Discord presence immediately on pause
- `Ignore advertisements`: sends no Discord update during advertisements
- `Enable autostart`: starts YTMPresence at Windows login
- `Copy token`: copies the current security token
- `Generate new token`: creates a new token and restarts the companion
- `Open log`: opens the current log file

## Building a release

### Development checks

Run the existing .NET checks and the extension suite from the repository root:

```powershell
dotnet build YTMPresence.slnx -c Release
dotnet test YTMPresence.Core.Tests/YTMPresence.Core.Tests.csproj -c Release --no-build
node --test
```

The extension tests use Node.js 20 or later and its built-in test runner, with no npm dependencies. They execute the actual extension scripts with browser, WebSocket and media/DOM fixtures under `tests/extension/`. Both CI and the release workflow run them. Live browser/Discord checks remain in [release QA](docs/RELEASE_QA.md).

### Packaging

Requirements: the .NET 10 SDK, and Inno Setup 6 (`ISCC.exe`) for the setup EXE. GitHub Actions installs Inno Setup automatically.

```powershell
.\scripts\package-release.ps1
```

If Inno Setup is not on the `PATH`, the compiler can be specified explicitly:

```powershell
.\scripts\package-release.ps1 -InnoSetupCompiler "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
```

The default build is framework-dependent, so the .NET 10 Desktop Runtime and the ASP.NET Core Runtime 10 have to be installed on any other machine.

For a larger package that brings the runtime along, as the release workflow and CI build it:

```powershell
.\scripts\package-release.ps1 -SelfContained
```

The script produces:

```text
artifacts\release\YTMPresence-<version>-win-x64-app\
artifacts\release\YTMPresence-extension.zip
artifacts\release\YTMPresence-extension-firefox.zip
artifacts\release\YTMPresence-<version>-win-x64.zip
artifacts\release\YTMPresence-<version>-win-x64-setup.exe
artifacts\release\extension\
artifacts\release\SHA256SUMS.txt
```

`YTMPresence-extension.zip` is the Chromium variant (Chrome/Edge); `YTMPresence-extension-firefox.zip` contains the same package with `browser_specific_settings` for Firefox (version 140 and later).

The setup EXE is built with Inno Setup in release builds and keeps the file name `YTMPresence-<version>-win-x64-setup.exe`, so that the in-app update flow stays compatible.

For ordinary distribution the setup EXE is the most convenient option. It installs YTMPresence without administrator rights to `%LOCALAPPDATA%\Programs\YTMPresence` and creates start menu shortcuts. The versioned full ZIP still contains the app, the extension and a `RELEASE.txt`.
The release script checks the package automatically after the build. Old versioned app and ZIP artifacts for the same runtime target are removed by default; `-KeepOldArtifacts` keeps them.
Before tagging, the [release QA checklist](docs/RELEASE_QA.md) helps to work through installation, update, uninstallation and GitHub release artifacts in order.

An existing release can also be verified separately:

```powershell
.\scripts\verify-release.ps1
```

Verification no longer rewrites anything: `SHA256SUMS.txt` is produced solely in `package-release.ps1`, once all artifacts are final. `verify-release.ps1` then checks that every artifact has exactly one matching hash line and that no stale, duplicate or unknown lines are present (`-RequireInstaller`/`-RequireSetup` additionally requires the setup EXE).

## Troubleshooting

### The companion does not start

If Windows reports that a .NET framework is missing, update to 1.5.0 or later, or install both runtimes listed under [Requirements](#requirements). A message about `Microsoft.AspNetCore.App` means the ASP.NET Core Runtime is missing.

### Discord shows nothing

- The Discord desktop app has to be running.
- The companion has to be visible in the tray.
- Reload the YouTube Music tab once.
- Check `Open diagnostics` in the tray.
- Check `Open log` in the tray.

### The extension is not connected

- Check the token in the extension.
- The companion URL is normally `ws://127.0.0.1:17373/ws`.
- Use `Test connection` in the extension options.
- Reload the YouTube Music tab after changing the token.

### Invalid token

If invalid tokens are being counted in the tray or the diagnostics window:

1. `Copy token`
2. Open the extension options
3. Replace the token
4. Save
5. Reload YouTube Music

### Port in use

The companion uses port `17373` by default. If that port is taken, it can be changed in the settings window or in `%APPDATA%\YTMPresence\settings.json`. The companion WebSocket URL in the extension has to be adjusted afterwards.

### Corrupt settings

If `settings.json` cannot be read as a valid configuration, YTMPresence preserves the file in the same folder as `settings.corrupt-<timestamp>.json` and starts with safe defaults and a new token. The new token then has to be saved in the extension again.

### Reporting a bug

If none of this helps, [open a bug report](https://github.com/Kentarohakase/YTMPresence/issues/new?template=bug_report.yml). It asks for both versions, the browser and the steps that lead to the problem. Never include the security token.
