# YTMPresence Roadmap

Reviewed **2026-11-02**. Baseline: **1.4.0**, released 2026-08-10 on GitHub and in the Chrome Web Store (`main` at `da60499`).

This roadmap replaces the draft of 2026-10-12. Release numbers are proposals for ordering the work, not promised dates.

## Where things stand

**Released in 1.4.0:** tray companion with mini player, diagnostics and verified updates; Chrome/Edge extension in the store; Firefox package for temporary loading; German and English interfaces and Discord text.

**Finished but not released:** the branch `codex/extension-tab-selection`, merged into `main` with [pull request #1](https://github.com/Kentarohakase/YTMPresence/pull/1).

- Within one browser profile, one YouTube Music tab owns the presence. Player commands go only to that tab, with a defined fallback on pause, close, reload, navigation, discard and background-worker restart.
- Progress is track-relative during autoplay; the previous track's elapsed time no longer leaks into the mini player and Discord.
- The green badge requires an accepted token on the current socket. Popup and options share the same actionable states.
- Four-step guided setup with store link, *Save and check*, and version/install-type display. The last German-only onboarding strings are localized.
- An extension test suite (92 tests, Node's built-in runner) runs in CI and the release workflow, next to 208 Core tests.
- Live checks in Edge passed; see the [live QA](docs/LIVE_QA_2026-10-12.md) and [setup QA](docs/SETUP_QA_2026-10-12.md) records.

**Open problems, checked against the code on 2026-11-02:**

| # | Problem | User impact | Priority |
| --- | --- | --- | --- |
| 1 | The published setup (2.2 MB) is framework-dependent and checks no prerequisites. The app needs the .NET 10 Desktop Runtime **and** the ASP.NET Core Runtime (`Microsoft.AspNetCore.App` in [Core](YTMPresence.Core/YTMPresence.Core.csproj)). The README names both since pull request #1. **For 1.5.0 the setup and the ZIP are self-contained and passed a clean install test without .NET.** | Users who arrive through the store listing and skip the README install a companion that does not start until they install the missing runtimes themselves. | P0 |
| 2 | The finished work above is not released. | Store and GitHub users don't get the fixes. | P0 |
| 3 | `CompanionServer.SendPlayerCommandAsync` sends every command to all authenticated connections. | With two browsers or profiles, one mini-player click controls both. | P1 |
| 4 | Discord `ERROR` replies are only logged ([DiscordIpcClient](YTMPresence.Core/DiscordIpcClient.cs)). Replies are not matched to the request nonce, and success is assumed once the frame is written. **Fixed in `main` for 1.6.0;** this also showed that Discord drops commands sent before READY, which cost the first presence after every connect. | Diagnostics can report success while Discord shows nothing. | P1 |
| 5 | The release workflow runs both test suites but not the extension archive check or `web-ext lint`; those run only in CI. The branch pins Node 20 in both workflows, which reached end of life in April 2026. **Fixed for 1.5.0.** | A tag can publish an archive that CI would have rejected. | P2 |
| 6 | The Firefox package declares `strict_min_version` 126 but uses `data_collection_permissions`, which needs Firefox 140, so `web-ext lint` warns. Firefox has no persistent install path. **Minimum raised to 140 for 1.5.0.** One lint warning remains for Firefox for Android, which only supports the key from 142; the package does not target Android, and 142 as the minimum would exclude Firefox ESR 140. | Firefox remains a developer option. | P2 |
| 7 | The Chrome Web Store listing exists only in German, although the extension defaults to English. English texts are already prepared in [CHROME_WEB_STORE.md](docs/CHROME_WEB_STORE.md). **English listing added with the 1.5.0 upload.** | English-speaking users see a German listing. | P2 |

## Principles

- Ship finished work before starting new work: one release at a time, each with its QA record.
- A working first start and reliable behavior come before features.
- Keep the small local model: no accounts, no cloud, no listening history.
- Every change that crosses components gets an automated test and a matching live check in [release QA](docs/RELEASE_QA.md).

## Now (no release needed)

- [x] Push `codex/extension-tab-selection` to GitHub and open [pull request #1](https://github.com/Kentarohakase/YTMPresence/pull/1) into `main`.
- [x] Name both runtimes with a download link in the README's installation section, until the self-contained setup from 1.5.0 is out.
- [x] Merge pull request #1 once CI passes.

## 1.5.0: ship what is done, and make it start on any PC

Goal: store and GitHub users get the branch work, and a new user's first start works.

- [ ] Finish the cheap open checks from the setup QA: German extension with English companion, a fresh browser profile, and one Chrome smoke run covering tab selection and setup.
- [x] Build the release setup self-contained (`package-release.ps1 -SelfContained` in the release workflow and CI). Measured on 2026-11-02: setup 61.8 MB instead of 2.2 MB, ZIP 88.7 MB, about 200 MB installed; well within the updater's 300 MB limit and 30-minute download timeout.
- [x] Test on a clean Windows VM without .NET: fresh install, update from 1.4.0 keeping settings and token, uninstall. Passed in Windows Sandbox with `scripts/test-clean-install.ps1`; see the [clean install QA](docs/CLEAN_INSTALL_QA_2026-11-02.md).
- [x] Translate the remaining German update-check messages in `UpdateChecker.cs` and the logger's first line; the sandbox log showed both. The download, checksum and setup-start messages are English now too, and the tray no longer shows the update prefix twice.
- [x] Translate the exception messages in `DiscordIpcClient.cs` (`Discord IPC nicht erreichbar`, `Discord IPC nicht verbunden`). The hard-coded German status texts in the settings, diagnostics and mini-player windows go through `UiText` now as well.
- [ ] Optional, to shrink the setup later: replace the ASP.NET Core WebSocket server with `HttpListener`, so the app no longer depends on `Microsoft.AspNetCore.App`.
- [x] Add the archive check and `web-ext lint` to the release workflow; move both workflows to a supported Node LTS.
- [x] Raise the Firefox `strict_min_version` to 140, which removes the desktop lint warning, and state the supported browser minimums in the README (Chrome/Edge 116+ for the WebSocket keepalive, Firefox 140+).
- [x] Bump to 1.5.0 and finish the changelog.
- [x] Tag `v1.5.0`; the release workflow published the GitHub release on 2026-11-02. All four artifacts match `SHA256SUMS.txt`; the setup has 61.7 MB.
- [x] Upload the extension to the Chrome Web Store and add the English listing. Submitted on 2026-11-02; GitHub and store status are recorded separately in the [store checklist](docs/STORE_SUBMISSION_CHECKLIST.md).
- [ ] Store review approves 1.5.0; update the status table.

**Done when:** 1.5.0 is live on GitHub and in the store; a clean VM reaches onboarding with the setup alone; updating from 1.4.0 keeps settings and token; `main` contains the branch work.

## 1.6.0: one source across browsers, confirmed Discord status

Goal: what the mini player and diagnostics show matches what actually happens in Discord, even with several browsers open.

- [ ] Select one source across companion connections, using the same rule as inside a browser: the connection with the playing track keeps ownership, otherwise the one that played most recently. Send commands to that connection only. Older extensions without a source ID fall back to "the last connection that sent a playing state".
- [x] Match Discord replies to the request nonce. Record *accepted*, *rejected* (with Discord's code and message) and *no reply* after a timeout, and show the result in diagnostics instead of assuming success. The client also waits for Discord's READY before sending; checked against the real Discord on 2026-11-02 (reply after READY in about 5 ms, none before it). Shipped early as 1.5.1, because the READY fix affects every 1.5.0 user.
- [ ] Show the selected source (browser, extension version), the last received track and the last Discord result in diagnostics. Exported diagnostics stay free of tokens.
- [ ] Give actionable companion messages for an occupied port and for Discord not running.
- [ ] Live checks: Chrome and Edge at the same time; Discord restart; companion restart without reloading the tab; Windows sleep/resume; background-worker restart and tab discard.

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
| ARM64 or other operating systems | There is real demand and a test machine; other operating systems need more than repackaging the WPF app. |

Not planned: cloud accounts, listening statistics, an interface rewrite.

## Keeping this roadmap current

After each release, delete the finished items (the changelog records them), recheck the open-problems table against the code, and update the review date at the top.
