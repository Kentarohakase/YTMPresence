# YTMPresence

YTMPresence shows the current YouTube Music track as Discord Rich Presence. The project consists of two parts:

- a local companion app in the tray that talks to Discord
- a browser extension that sends the current YouTube Music state to the companion

The extension is available in the Chrome Web Store:
[YTM Discord Presence Bridge](https://chromewebstore.google.com/detail/ytm-discord-presence-brid/obdinnbalnbmpkcaopnhnkdgdkcgligi)

The extension requires the Windows companion app from the [releases](https://github.com/Kentarohakase/YTMPresence/releases), since a browser cannot set Discord Rich Presence.

GitHub releases and Chrome Web Store updates are published separately. See the dated [release status](docs/STORE_SUBMISSION_CHECKLIST.md#release-status) for confirmed releases, recorded submissions and unverified store status.

## Installation

### Requirements

- Windows (x64) with the Discord desktop app.
- Chrome or Edge 116 or later, or Firefox 140 or later.
- Version 1.5.0 and later include .NET in the setup and ZIP; no separate runtime installation is needed.
- Version 1.4.0 and earlier require both x64 runtimes from the [.NET 10 download page](https://dotnet.microsoft.com/download/dotnet/10.0):
  - **.NET Desktop Runtime 10**
  - **ASP.NET Core Runtime 10**

  The Desktop Runtime alone is not enough; without the ASP.NET Core Runtime the companion does not start.

### 1. Start the companion

Run `YTMPresence.exe`. Its tray icon appears in the Windows notification area.

On first start, the app generates a security token and saves its settings to:

```text
%APPDATA%\YTMPresence\settings.json
```

### 2. Follow the four setup steps

Open **Getting started** from the companion tray menu. The guide shows the companion version and these steps:

1. **Install the extension.** For Chrome/Edge, use [YTM Discord Presence Bridge in the Chrome Web Store](https://chromewebstore.google.com/detail/ytm-discord-presence-brid/obdinnbalnbmpkcaopnhnkdgdkcgligi). Firefox uses the [instructions below](#firefox-desktop).
2. **Copy the token** in the guide or through the tray menu.
3. **Save and check in the extension options.** Paste the token, then choose **Save and check**. The extension saves the settings and waits for the companion to accept the token on the active connection. The default URL is `ws://127.0.0.1:17373/ws`, with `localhost` as a fallback when no custom URL is saved. If you changed the companion's address, enter its displayed URL in the extension's **Advanced settings**.
4. **Open YouTube Music.** After switching extensions, reload existing [YouTube Music tabs](https://music.youtube.com/), then start a track and check the visible presence in Discord.

These setup controls apply to extension 1.5.0 and later. Version 1.4.0 has separate **Save** and **Test connection** buttons on its options page; its popup also uses **Test connection**.

**Each browser profile and each store/local installation has its own settings.** Copy the token into the options of the installation you actually use. Enable only one YTMPresence extension per profile. A version number alone cannot distinguish an unreleased checkout from the published extension.

The popup and options show the extension version, installation type and companion version, if available. Installation types are “Package installation”, “Local/unpacked”, “Managed” and “Unknown”; “Package installation” does not confirm a store installation. A missing companion version appears as “Not reported”. Version differences alone do not prevent a connection.

| Status or result | Next action |
| --- | --- |
| Token missing | Copy the current token from the companion; paste it in the extension options and save and check. |
| Connecting / Checking token | Keep the companion running and wait for the four-second check. Opening a socket alone does not produce a green badge. |
| Saved – token rejected | The settings were saved, but authentication failed. Copy the current companion token and save and check again. |
| Saved – companion not reachable / Connection interrupted | Start the companion and retry. If needed, check the advanced URL. Network failures reconnect automatically with delays from 500 ms to 10 seconds. |
| Not saved | Reopen options and retry saving. The previously saved connection has not been replaced by the failed write. |
| Companion connected, no track data | Open YouTube Music or reload its existing tab and start a track. |

Unsaved changes are marked in the options. Connection status and the popup's **Test connection** use the saved settings. A working companion connection does **not** confirm that Discord accepted or displays the presence; check Discord itself.

The extension follows the browser language and the companion follows the Windows language. Both support German and English. Choose **Later** to leave setup or **Done** to mark it complete manually.

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

Within one browser profile, the extension selects one tab for both the displayed track and mini-player commands. It stays selected while playing; paused background tabs cannot replace it. If it pauses, another playing tab takes over. If none is playing, Play resumes the selected paused tab.

If the selected tab closes, reloads, navigates away or is discarded, another available tab takes over. After a background-worker restart, the extension retrieves the latest state from open tabs and prefers a playing tab. When no source remains, the popup clears its track and the companion clears Discord presence after the idle timeout (120 seconds by default).

### Several browsers or profiles

The behavior below is implemented in `main` but is not included in the published 1.5.1 release.

The same selection rule applies across browsers and profiles. A playing source stays selected; if it pauses, another playing source takes over. If it closes, an available source takes over immediately. Only the selected source receives mini-player commands. The diagnostics window identifies it, for example "Edge 155 #3".

## Changelog

All release changes are listed in [CHANGELOG.md](CHANGELOG.md).

Planned improvements, priorities and completion criteria are tracked in [ROADMAP.md](ROADMAP.md).

## Publishing the extension

Store copy and privacy information:

- [Chrome Web Store](docs/CHROME_WEB_STORE.md)
- [Extension privacy notice](docs/EXTENSION_PRIVACY.md)
- [Store submission checklist](docs/STORE_SUBMISSION_CHECKLIST.md)

## Updates

Choose `Check for updates` in the tray menu to check the latest GitHub release. If it includes a setup EXE, the app can download it, verify it and launch the installer. The app also checks once at startup when `CheckForUpdatesOnStartup` is enabled.

Updates use the following checks and safeguards:

- **Mandatory SHA256 verification**: the release must include `SHA256SUMS.txt` with exactly one entry matching the setup file name and computed hash. A missing checksum file or entry, duplicate or conflicting entries, or an invalid or mismatched hash blocks installation.
- **Permitted update sources**: updates are downloaded only over HTTPS from `api.github.com`, `github.com`, `release-assets.githubusercontent.com` and the older asset host `objects.githubusercontent.com`. The initial URL, every redirect and the final target are checked. HTTP, direct IP addresses, loopback, private and link-local targets, and other hosts are rejected. Standard TLS certificate validation applies; there is no custom DNS pinning. For local QA only, `scripts/test-update-flow.ps1` can use an HTTP loopback source through the explicit `AllowLoopbackUpdateSource` setting.
- **Size limits and timeouts**: downloads are limited to 300 MB for setup, 64 KB for checksums and 4 MB for release metadata. Limits are checked against Content-Length before downloading and enforced while streaming. Connection, stall and overall timeouts apply. Cancellation through the window or app shutdown also triggers cleanup.
- **Temporary files**: each update attempt uses a separate directory under `%TEMP%\YTMPresence\updates`. Downloads use a `.part` file, renamed only after a successful hash check; a `.part` file is never launched. Errors, cancellation, closing the window and app shutdown abort ongoing I/O and attempt cleanup. If a running setup still locks its file, the orphaned directory is removed by age on a later app start.
- **Verification immediately before launch**: existence, file name, size and SHA256 are checked again before setup starts. The file stays locked against modification during launch. YTMPresence exits only after setup starts and remains usable if launch fails.

**Limits of SHA256 verification**: `SHA256SUMS.txt` comes from the same GitHub release as the setup. The check detects transfer errors and changes to individual files, but cannot authenticate the release itself. An attacker who replaces both the setup **and** its checksum file, for example through a compromised release account, would not be detected. Checksums do not replace a digital signature or publisher attestation.

**Stopping the running app during installation**: the installer asks the instance running from the target directory to shut down through a path-bound local mechanism. If it does not respond, Windows Restart Manager closes processes holding files open in the installation folder. Processes are not terminated by file name, and a portable copy running from another folder is left untouched.

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

Run the .NET build and both test suites from the repository root:

```powershell
dotnet build YTMPresence.slnx -c Release
dotnet test YTMPresence.Core.Tests/YTMPresence.Core.Tests.csproj -c Release --no-build
node --test
```

The extension tests require Node.js 20 or later and use its built-in test runner without npm dependencies. They run the extension scripts with browser, WebSocket and media/DOM fixtures under `tests/extension/`. CI and the release workflow both run these tests. See [release QA](docs/RELEASE_QA.md) for live browser and Discord checks.

### Packaging

Requirements: the .NET 10 SDK, and Inno Setup 6 (`ISCC.exe`) for the setup EXE. GitHub Actions installs Inno Setup automatically.

```powershell
.\scripts\package-release.ps1
```

If Inno Setup is not on the `PATH`, specify the compiler location:

```powershell
.\scripts\package-release.ps1 -InnoSetupCompiler "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
```

The default build is framework-dependent and requires both the .NET 10 Desktop Runtime and the ASP.NET Core Runtime 10 on the target machine.

To include the runtime, as CI and the release workflow do, build a larger self-contained package:

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

Release builds use Inno Setup and retain the file name `YTMPresence-<version>-win-x64-setup.exe` for compatibility with the in-app updater.

Use the setup EXE for ordinary distribution. It installs YTMPresence to `%LOCALAPPDATA%\Programs\YTMPresence` without administrator rights and creates start menu shortcuts. The versioned full ZIP contains the app, extension and `RELEASE.txt`.
The release script verifies the package after building. It removes old versioned app and ZIP artifacts for the same runtime target unless `-KeepOldArtifacts` is set.
Before tagging, complete the [release QA checklist](docs/RELEASE_QA.md) for installation, updates, uninstallation and GitHub release artifacts.

To verify an existing release:

```powershell
.\scripts\verify-release.ps1
```

`package-release.ps1` creates `SHA256SUMS.txt` after all artifacts are final. `verify-release.ps1` checks that each artifact has exactly one matching hash entry, with no stale, duplicate or unknown entries. It does not modify files. Pass `-RequireInstaller`/`-RequireSetup` to require the setup EXE as well.

## Troubleshooting

### The companion does not start

If Windows reports that a .NET framework is missing, update to 1.5.0 or later, or install both runtimes listed under [Requirements](#requirements). A message about `Microsoft.AspNetCore.App` means the ASP.NET Core Runtime is missing.

### Discord shows nothing

- Make sure the Discord desktop app is running.
- Check that the companion icon is visible in the tray.
- Reload the YouTube Music tab once.
- Check `Open diagnostics` in the tray.
- Check `Open log` in the tray.

### The extension is not connected

- Check the token in the extension.
- The companion URL is normally `ws://127.0.0.1:17373/ws`.
- In the extension options, choose **Save and check** to save your inputs and test the connection. To test the saved settings, use **Test connection** in the popup.
- Reload the YouTube Music tab after changing the token.

### Invalid token

If the tray menu or diagnostics window reports invalid tokens:

1. `Copy token`
2. Open the extension options
3. Replace the token
4. Choose **Save and check**
5. Reload YouTube Music

### Port in use

The companion uses port `17373` by default. If it is occupied, change the port in the settings window or `%APPDATA%\YTMPresence\settings.json`, then update the companion WebSocket URL in the extension.

### Corrupt settings

If `settings.json` is invalid, YTMPresence keeps a copy in the same folder as `settings.corrupt-<timestamp>.json` and starts with safe defaults and a new token. Save the new token in the extension to reconnect.

### Reporting a bug

If the problem persists, [open a bug report](https://github.com/Kentarohakase/YTMPresence/issues/new?template=bug_report.yml) with the companion and extension versions, your browser and steps to reproduce it. Never include the security token.
