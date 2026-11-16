# YTMPresence Roadmap

Planning reviewed **2026-11-15**; release status checked **2026-11-15**. Planning baseline: **1.6.0** (`main` at `814028c`), published on GitHub on 2026-11-15. The previous review on 2026-11-02 started from 1.4.0.

This roadmap replaces the draft of 2026-10-12. Release numbers indicate the proposed order of work; they do not promise delivery dates.

## Where things stand

**Published on GitHub:** [1.5.0](https://github.com/Kentarohakase/YTMPresence/releases/tag/v1.5.0) and [1.5.1](https://github.com/Kentarohakase/YTMPresence/releases/tag/v1.5.1), both on 2026-11-02, and [1.6.0](https://github.com/Kentarohakase/YTMPresence/releases/tag/v1.6.0), [1.6.1](https://github.com/Kentarohakase/YTMPresence/releases/tag/v1.6.1), [1.6.2](https://github.com/Kentarohakase/YTMPresence/releases/tag/v1.6.2), [1.6.3](https://github.com/Kentarohakase/YTMPresence/releases/tag/v1.6.3) and [1.6.4](https://github.com/Kentarohakase/YTMPresence/releases/tag/v1.6.4), all on 2026-11-15. The maintainer confirmed on 2026-11-12 that 1.5.0 is live in the Chrome Web Store. Version 1.5.1 changes only the companion; the maintainer reported submitting the 1.6.0 extension for store review on 2026-11-15. The 1.6.1 to 1.6.4 extensions have not been uploaded to the store. See the [store checklist](docs/STORE_SUBMISSION_CHECKLIST.md#release-status) for the evidence and its limits.

**Released in 1.4.0:** tray companion with mini player, diagnostics and verified updates; Chrome/Edge extension in the store; Firefox package for temporary loading; German and English interfaces and Discord text.

**Published on GitHub in 1.5.0:** the tab-selection branch, merged into `main` with [pull request #1](https://github.com/Kentarohakase/YTMPresence/pull/1).

- Within one browser profile, one YouTube Music tab supplies the presence and receives player commands. Selection follows defined rules on pause, close, reload, navigation, discard and background-worker restart.
- Autoplay progress is relative to the current track; the mini player and Discord no longer include the previous track's elapsed time.
- The green badge requires an accepted token on the current socket. Popup and options use the same status messages and next steps.
- Four-step setup guide with a store link, *Save and check*, and version and installation-type information. The remaining German-only onboarding strings are localized.
- The extension suite (92 tests, Node's built-in runner) runs alongside 208 Core tests in CI and the release workflow.
- Live checks in Edge passed; see the [live QA](docs/LIVE_QA_2026-10-12.md) and [setup QA](docs/SETUP_QA_2026-10-12.md) records.

**Published on GitHub in 1.6.0:** one source across browsers and profiles, with player commands sent only to that source; Discord's answer to each update, the selected source and the extension version in diagnostics; clear messages for an occupied port and a closed Discord; the track's cover as the Discord image. Discord reply matching and the READY fix shipped early in 1.5.1. Multi-browser and recovery checks passed on 2026-11-12 (see the [live QA](docs/LIVE_QA_2026-11-12.md)), album covers and music videos on 2026-11-14; the maintainer reported both results. Details are in the [changelog](CHANGELOG.md#160---2026-11-15).

**Published on GitHub in 1.6.1 on 2026-11-15:** the three finished extension items from the 1.7.0 plan below (companion version notice, **Copy diagnostics**, welcome page on first install), the shorter Discord status line and the cover host check in the mini player; see the [changelog](CHANGELOG.md#161---2026-11-15). The live checks from [release QA](docs/RELEASE_QA.md) are not recorded for this release.

**Published on GitHub in 1.6.2 on 2026-11-15:** the song title in Discord's status line and the new logo for companion, extension and installer; see the [changelog](CHANGELOG.md#162---2026-11-15). On 2026-11-15 the maintainer reported that the member list shows the song title. The new tray and setup icons have not been checked live.

**Published on GitHub in 1.6.3 on 2026-11-15:** a corrected tooltip for the status line option; see the [changelog](CHANGELOG.md#163---2026-11-15). On the same day the Discord application was renamed to "YTM Presence" and its icon and the `ytm_large`, `play` and `pause` images were replaced in the Developer Portal; that applies to all versions. The 1.6.3 extension is identical to 1.6.2 apart from its version number.

**Published on GitHub in 1.6.4 on 2026-11-15:** the song title in Discord links to the track; the tray menu stays open during the update check, and the update notification opens the update window; the presence returns after a Discord restart during a pause, and seeks reach Discord's progress bar right away; German texts address the user as "du". See the [changelog](CHANGELOG.md#164---2026-11-15). On 2026-11-15 the maintainer reported that a local test build of the update menu, the update notification and the title link worked. The reconnect and seek fixes have no recorded live check.

**Prepared as 1.6.5 on 2026-11-16:** Discord shows "Listening to YouTube Music" instead of "Listening to YTM Presence"; the companion sends the name in the activity's `name` field because the verified Discord application can no longer be renamed freely. See the [changelog](CHANGELOG.md#165---2026-11-16). On 2026-11-15 the maintainer reported that a local test build showed "Listening to YouTube Music" in Discord. A second account and older Discord clients were not checked. The 1.6.5 extension is identical to 1.6.4 apart from its version number. GitHub publication is not yet recorded.

**Problems identified on 2026-11-02 and their release status:**

| # | Problem | User impact | Priority |
| --- | --- | --- | --- |
| 1 | The 1.4.0 setup (2.2 MB) is framework-dependent and checks no prerequisites. It needs the .NET 10 Desktop Runtime **and** the ASP.NET Core Runtime (`Microsoft.AspNetCore.App` in [Core](YTMPresence.Core/YTMPresence.Core.csproj)). The README names both since pull request #1. **Published in 1.5.0: self-contained setup and ZIP, with a clean install test passed without .NET.** | Users installing 1.4.0 still need both runtimes; 1.5.0 and later include them. | P0 |
| 2 | The finished work above is published on GitHub in 1.5.0. **The maintainer confirmed on 2026-11-12 that 1.5.0 is live in the Chrome Web Store.** | GitHub and store users can get the fixes. | P0 |
| 3 | In 1.5.1, `CompanionServer.SendPlayerCommandAsync` sends every command to all authenticated connections. **Fixed and published in 1.6.0.** | With two browsers or profiles on 1.5.1, one mini-player click controls both. | P1 |
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

## Next: finish the 1.6.0 rollout (no release needed)

Goal: store users get the 1.6.0 extension, and the store pages describe the current data flow.

- [x] Upload the 1.6.0 extension to the Chrome Web Store. Submitted for review on 2026-11-15, as reported by the maintainer.
- [ ] Check that the dashboard's data-flow text and privacy declarations match the English and German wording in [CHROME_WEB_STORE.md](docs/CHROME_WEB_STORE.md), which now names the cover URL passed to Discord. Not recorded with the submission.
- [ ] Confirm approval and publication of 1.6.0 and update the [release status table](docs/STORE_SUBMISSION_CHECKLIST.md#release-status).
- [ ] Confirm that the English store listing is visible to store users; this has been open since the 1.5.0 submission.
- [ ] Optionally replace the Discord screenshot on the store page with one that shows album art.
- [ ] Record browser, Discord, companion build and Windows versions at the next live check, as the [2026-11-12 live QA](docs/LIVE_QA_2026-11-12.md) asks.

**Done when:** the [release status table](docs/STORE_SUBMISSION_CHECKLIST.md#release-status) records 1.6.0 as published in the store, and the listing's data-flow text matches the [privacy notice](docs/EXTENSION_PRIVACY.md).

## 1.7.0: support and compatibility

Goal: when something goes wrong, users see which versions are involved and what to do next, and a bug report carries enough detail to act on without the token.

- [x] Show a notice in the popup and options when the connected companion is older than the version the extension was tested with, or reports no version, with a link to the GitHub releases. Published in 1.6.1.
- [x] Add **Copy diagnostics** to the extension popup: versions, installation type, browser, connection and track state, without the token or track titles. When the clipboard is unavailable, show the text for manual copying. Published in 1.6.1.
- [x] Open the setup guide on first install when no token is stored; updates leave existing installations alone. Published in 1.6.1.
- [ ] Test and document compatibility across versions (an older store extension with a newer companion and the reverse), since the store and GitHub update separately. Include album art with an extension older than 1.6.0, which still shortens long cover URLs.
- [x] Add a bug-report issue template asking for both versions, the browser, reproduction steps and an optional redacted diagnostics export.
- [ ] Review keyboard navigation, focus order and high-DPI scaling in settings and the mini player; onboarding already passed its keyboard check.
- [ ] Keep a short live QA matrix per release (Chrome, Edge, Firefox × track change, pause/clear, ads, source switch, restarts, album art), recorded with versions under `docs/`.

**Done when:** the extension points out an outdated companion, the extension and companion diagnostics together are enough to triage a report, and the 1.7.0 live QA matrix is recorded with versions.

## Later, only with demand

| Opportunity | Start when |
| --- | --- |
| Signed Firefox add-on on addons.mozilla.org | Firefox users ask for it; it adds a second store release path to maintain. |
| Code-signed setup and publisher check in the updater | A signing certificate and its renewal are affordable. Keep the existing checksum verification. |
| Pairing without copying the token | Feedback after 1.5.0 still shows the token step as the main obstacle. Evaluate a short-lived local pairing code that keeps authentication. |
| Presence presets, a quick privacy toggle, hotkeys | Concrete user requests show which one is worth building. |
| Sharper images for music videos | Users find the 320×180 thumbnail too soft. Larger 16:9 thumbnails exist only for some videos, so this needs an availability check, preferably in the browser, which already loads YouTube images, rather than a new companion request. |
| Microsoft Edge Add-ons listing | Edge users ask for it. Edge already installs the extension from the Chrome Web Store, and a second listing adds an upload and review per release. |
| ARM64 or other operating systems | Users request support and a test machine is available; other operating systems require changes beyond repackaging the WPF app. |

Not planned: cloud accounts, listening statistics, an interface rewrite.

## Keeping this roadmap current

After each release, delete the finished items (the changelog records them), recheck the open-problems table against the code, and update the review date at the top.
