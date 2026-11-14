# YTMPresence Roadmap

Planning reviewed **2026-11-02**; release status checked **2026-11-12**. Planning baseline: **1.4.0** (`main` at `da60499`), published on GitHub on 2026-08-10 and recorded as published in the Chrome Web Store dashboard on 2026-11-02.

This roadmap replaces the draft of 2026-10-12. Release numbers indicate the proposed order of work; they do not promise delivery dates.

## Where things stand

**Published on GitHub:** [1.5.0](https://github.com/Kentarohakase/YTMPresence/releases/tag/v1.5.0) and [1.5.1](https://github.com/Kentarohakase/YTMPresence/releases/tag/v1.5.1), both on 2026-11-02. The maintainer confirmed on 2026-11-12 that 1.5.0 is live in the Chrome Web Store. Version 1.5.1 changes only the companion. See the [store checklist](docs/STORE_SUBMISSION_CHECKLIST.md#release-status) for the evidence and its limits.

**Released in 1.4.0:** tray companion with mini player, diagnostics and verified updates; Chrome/Edge extension in the store; Firefox package for temporary loading; German and English interfaces and Discord text.

**Published on GitHub in 1.5.0:** the tab-selection branch, merged into `main` with [pull request #1](https://github.com/Kentarohakase/YTMPresence/pull/1).

- Within one browser profile, one YouTube Music tab supplies the presence and receives player commands. Selection follows defined rules on pause, close, reload, navigation, discard and background-worker restart.
- Autoplay progress is relative to the current track; the mini player and Discord no longer include the previous track's elapsed time.
- The green badge requires an accepted token on the current socket. Popup and options use the same status messages and next steps.
- Four-step setup guide with a store link, *Save and check*, and version and installation-type information. The remaining German-only onboarding strings are localized.
- The extension suite (92 tests, Node's built-in runner) runs alongside 208 Core tests in CI and the release workflow.
- Live checks in Edge passed; see the [live QA](docs/LIVE_QA_2026-10-12.md) and [setup QA](docs/SETUP_QA_2026-10-12.md) records.

**Problems identified on 2026-11-02 and their release status:**

| # | Problem | User impact | Priority |
| --- | --- | --- | --- |
| 1 | The 1.4.0 setup (2.2 MB) is framework-dependent and checks no prerequisites. It needs the .NET 10 Desktop Runtime **and** the ASP.NET Core Runtime (`Microsoft.AspNetCore.App` in [Core](YTMPresence.Core/YTMPresence.Core.csproj)). The README names both since pull request #1. **Published in 1.5.0: self-contained setup and ZIP, with a clean install test passed without .NET.** | Users installing 1.4.0 still need both runtimes; 1.5.0 and later include them. | P0 |
| 2 | The finished work above is published on GitHub in 1.5.0. **The maintainer confirmed on 2026-11-12 that 1.5.0 is live in the Chrome Web Store.** | GitHub and store users can get the fixes. | P0 |
| 3 | In 1.5.1, `CompanionServer.SendPlayerCommandAsync` sends every command to all authenticated connections. **Fixed in `main` for 1.6.0; not yet published.** | With two browsers or profiles on 1.5.1, one mini-player click controls both. | P1 |
| 4 | In 1.5.0, Discord `ERROR` replies were only logged ([DiscordIpcClient](YTMPresence.Core/DiscordIpcClient.cs)). Replies were not matched to the request nonce, and success was assumed once the frame was written. **Published in 1.5.1:** reply matching and waiting for READY before sending, which fixes the lost first presence after each connection. | Older companions can report success while Discord shows nothing. | P1 |
| 5 | During 1.5.0 preparation, the release workflow ran both test suites but omitted the extension archive check and `web-ext lint`, which ran only in CI. Both workflows pinned Node 20, which reached end of life in April 2026. **Fixed and published in 1.5.0.** | Earlier release workflows could publish an archive that CI would have rejected. | P2 |
| 6 | Before 1.5.0, the Firefox package declared `strict_min_version` 126 but used `data_collection_permissions`, which needs Firefox 140, so `web-ext lint` warned. **Minimum raised to 140 and published in 1.5.0.** One lint warning remains for Firefox for Android, which only supports the key from 142; the package does not target Android, and 142 as the minimum would exclude Firefox ESR 140. Firefox still has no persistent install path. | Firefox remains a developer option. | P2 |
| 7 | The store listing was originally submitted only in German, although the extension defaults to English. **The 2026-11-02 record says an English listing was added to the 1.5.0 submission.** See [CHROME_WEB_STORE.md](docs/CHROME_WEB_STORE.md); current publication of that submission is unverified. | Confirm that the submitted English listing is available to store users. | P2 |

## Principles

- Ship finished work before starting new work: one release at a time, each with its QA record.
- A working first start and reliable behavior come before features.
- Keep the app local, without accounts, cloud services or listening history.
- Every change that crosses components gets an automated test and a matching live check in [release QA](docs/RELEASE_QA.md).

## Now (no release needed)

- [x] Push the tab-selection branch to GitHub and open [pull request #1](https://github.com/Kentarohakase/YTMPresence/pull/1) into `main`.
- [x] List both runtimes and their download link in the README's installation section until the self-contained 1.5.0 setup is released.
- [x] Merge pull request #1 once CI passes.

## 1.5.0: ship what is done, and make it start on any PC

Goal: release the completed branch work to store and GitHub users, and let new users start the app without installing runtimes separately.

- [x] Complete the remaining setup QA checks: German extension with English companion, a fresh browser profile, and one Chrome smoke run covering tab selection and setup. Passed on 2026-11-12 as reported by the maintainer; see the [live QA](docs/LIVE_QA_2026-11-12.md).
- [x] Build the release setup self-contained (`package-release.ps1 -SelfContained` in the release workflow and CI). Measured on 2026-11-02: setup 61.8 MB instead of 2.2 MB, ZIP 88.7 MB, about 200 MB installed; well within the updater's 300 MB limit and 30-minute download timeout.
- [x] Test on a clean Windows VM without .NET: fresh install, update from 1.4.0 keeping settings and token, uninstall. Passed in Windows Sandbox with `scripts/test-clean-install.ps1`; see the [clean install QA](docs/CLEAN_INSTALL_QA_2026-11-02.md).
- [x] Translate the remaining German messages found in the sandbox log: update-check errors in `UpdateChecker.cs` and the logger's first line. Download, checksum and setup-start messages are also now English, and tray errors no longer repeat the update prefix.
- [x] Translate the exception messages in `DiscordIpcClient.cs` (`Discord IPC nicht erreichbar`, `Discord IPC nicht verbunden`). The hard-coded German status texts in the settings, diagnostics and mini-player windows go through `UiText` now as well.
- [ ] Optional later change to reduce setup size: replace the ASP.NET Core WebSocket server with `HttpListener` to remove the dependency on `Microsoft.AspNetCore.App`.
- [x] Add the archive check and `web-ext lint` to the release workflow; move both workflows to a supported Node LTS.
- [x] Raise the Firefox `strict_min_version` to 140, which removes the desktop lint warning, and state the supported browser minimums in the README (Chrome/Edge 116+ for the WebSocket keepalive, Firefox 140+).
- [x] Bump to 1.5.0 and finish the changelog.
- [x] Tag `v1.5.0`; the release workflow published the GitHub release on 2026-11-02. All four artifacts match `SHA256SUMS.txt`; the setup has 61.7 MB.
- [x] Upload the extension to the Chrome Web Store and add the English listing. Submitted on 2026-11-02; GitHub and store status are recorded separately in the [store checklist](docs/STORE_SUBMISSION_CHECKLIST.md).
- [x] Verify store approval and publication of 1.5.0; update the status table. The maintainer confirmed publication on 2026-11-12. The English listing is not yet confirmed separately.

**Done when:** 1.5.0 is live on GitHub and in the store; a clean VM reaches onboarding with the setup alone; updating from 1.4.0 keeps settings and token; `main` contains the branch work.

## 1.6.0: one source across browsers, confirmed Discord status

Goal: what the mini player and diagnostics show matches what actually happens in Discord, even with several browsers open.

- [x] Select one source across companion connections, using the same rule as inside a browser: the connection with the playing track keeps ownership, otherwise the one that played most recently. Send commands to that connection only. Each extension connection already carries one selected tab, so the selection works per connection without a protocol change, also for older extensions.
- [x] Match Discord replies to the request nonce. Record *accepted*, *rejected* (with Discord's code and message) and *no reply* after a timeout, and show the result in diagnostics instead of assuming success. The client also waits for Discord's READY before sending; checked against the real Discord on 2026-11-02 (reply after READY in about 5 ms, none before it). Shipped early as 1.5.1, because the READY fix affects every 1.5.0 user.
- [x] Show the selected source (browser, extension version), the last received track and the last Discord result in diagnostics. Exported diagnostics stay free of tokens. The source is named after the browser in the User-Agent; the extension reports its version with the connection test.
- [x] Explain how to resolve an occupied port and what happens when Discord is not running.
- [x] Show the track's cover as the Discord image, with the 16:9 thumbnail for music videos and a setting to turn it off. Added during release preparation; on 2026-11-14 the maintainer reported that album covers and music videos display correctly in Discord.
- [x] Live checks: Chrome and Edge at the same time; Discord restart; companion restart without reloading the tab; Windows sleep/resume; background-worker restart and tab discard. Passed on 2026-11-12 with a companion built from `main`, as reported by the maintainer; see the [live QA](docs/LIVE_QA_2026-11-12.md).

**Done when:** with two browsers, one click affects one source and the displayed track matches it; a rejected activity shows as rejected; every listed recovery scenario restores the presence without reloading YouTube Music.

## 1.7.0: support and polish

- [ ] Test and document compatibility across versions (an older store extension with a newer companion and the reverse), since the store and GitHub update separately.
- [x] Add a bug-report issue template asking for both versions, the browser, reproduction steps and an optional redacted diagnostics export.
- [ ] Review keyboard navigation, focus order and high-DPI scaling in settings and the mini player; onboarding already passed its keyboard check.
- [ ] Keep a short live QA matrix per release (Chrome, Edge, Firefox × track change, pause/clear, ads, source switch, restarts), recorded with versions under `docs/`.

## Later, only with demand

| Opportunity | Start when |
| --- | --- |
| Signed Firefox add-on on addons.mozilla.org | Firefox users ask for it; it adds a second store release path to maintain. |
| Code-signed setup and publisher check in the updater | A signing certificate and its renewal are affordable. Keep the existing checksum verification. |
| Pairing without copying the token | Feedback after 1.5.0 still shows the token step as the main obstacle. Evaluate a short-lived local pairing code that keeps authentication. |
| Presence presets, a quick privacy toggle, hotkeys | Concrete user requests show which one is worth building. |
| ARM64 or other operating systems | Users request support and a test machine is available; other operating systems require changes beyond repackaging the WPF app. |

Not planned: cloud accounts, listening statistics, an interface rewrite.

## Keeping this roadmap current

After each release, delete the finished items (the changelog records them), recheck the open-problems table against the code, and update the review date at the top.
