# YTMPresence Roadmap

YTMPresence already covers the main workflow: YouTube Music playback reaches Discord through a local Windows companion, with a mini player, diagnostics, updates and German/English interfaces. The next releases should make that workflow predictable across multiple tabs, reconnects and first-time installation.

Planning baseline: **1.4.0**, commit `da60499`, reviewed **2026-10-12**. Completed implementation items are checked below; unchecked items remain planned. Milestones describe delivery order, not promised dates or assigned release versions.

Implemented since that baseline: extension regression tests and selection within one browser profile, including recovery after background-worker restart. An [Edge/Discord live run and local-extension retest](docs/LIVE_QA_2026-10-12.md) found and verified the fix for an autoplay timing defect. Single-tab commands, source switching and reload/navigation/closure fallback passed live in Edge. Guided onboarding, save-and-check on the authenticated background socket, installation metadata and optional companion-version reporting are also implemented; see the [setup QA report](docs/SETUP_QA_2026-10-12.md). The remaining live matrix and selection across companion connections remain open.

## Assessment of the baseline

The existing foundation is worth keeping:

- Core logic is separated from the WPF interface and extension. Settings recovery, token authentication, update verification and release packaging already have substantial checks.
- The local Release build passes with zero warnings and errors; all **207 Core tests** pass. Version consistency also passes. These results do not establish live browser/Discord compatibility or successful installation on a clean Windows machine.
- CI builds the solution, runs Core tests, verifies release archives and lints the packaged Firefox extension. The [release QA checklist](docs/RELEASE_QA.md) covers installation and updates.

At the baseline, the largest gaps were in coordination between components and proof of user-visible behavior:

| Finding in the current code | User impact | Priority |
| --- | --- | --- |
| The extension ignores the sender tab when receiving state and forwards player commands to every YouTube Music tab; the companion also broadcasts commands to authenticated connections. | Multiple tabs or browser profiles can compete for the displayed track and respond to one button press. | P1 |
| The extension marks an open socket as connected before token validation; regular keepalive responses are not used to update authentication status. | A green connection badge can coexist with a rejected token. | P1 |
| Discord error responses are logged, while presence success is recorded after writing a frame. | Diagnostics can report success even when Discord rejects the activity. | P1 |
| There is no automated extension behavior suite in the repository or CI. | Playback extraction, pause handling, routing and reconnect regressions lack direct automated coverage. | P1 |
| Release packaging defaults to framework-dependent deployment. The tray depends on Windows Desktop and ASP.NET Core shared frameworks, but the README mentions only the Desktop Runtime. | A developer machine is insufficient evidence that the downloaded app starts for a new user. | P1 |
| Several onboarding feedback/error strings remain hard-coded in German. | English users still encounter untranslated messages despite the 1.4.0 localization work. | P2 |

Evidence: [extension background](YTMPresence/extension/background.js), [companion command routing](YTMPresence.Core/CompanionServer.cs), [Discord IPC](YTMPresence.Core/DiscordIpcClient.cs), [presence status](YTMPresence.Core/PresenceService.cs), [CI](.github/workflows/ci.yml), [Core dependencies](YTMPresence.Core/YTMPresence.Core.csproj), [tray dependencies](YTMPresence.Tray.Wpf/YTMPresence.Tray.Wpf.csproj), [packaging](scripts/package-release.ps1), [onboarding](YTMPresence.Tray.Wpf/OnboardingWindow.xaml.cs).

## Milestone 1 Reliable playback and status

### Add extension regression tests

- [x] Introduce a small JavaScript test harness for state extraction, message handling and command routing. Use deterministic media/DOM fixtures and mocked browser/WebSocket APIs; keep live YouTube Music checks separate.
- [x] Cover playing/paused media, audio/video elements, seeks, advertisements, missing metadata, track URL selection and encoded URLs. Reproduce the playback and URL failures recorded in the [changelog](CHANGELOG.md).
- [x] Cover selected-tab routing, reconnect with pending state, token changes, obsolete-socket commands, tab lifecycle and background-worker recovery. Run the suite in PR CI and the release workflow.
- [x] Extend coverage to rejected-token status, reconnect without new playback data and the remaining socket callback races alongside the connection-status work below.

**Done when:** tests fail for the reproduced regressions and pass with the intended behavior. Lint and archive verification remain in place.

### Select one playback source

- [x] Track the sender tab for each state within one browser profile. Keep the playing selection; otherwise prefer a playing fallback, retain a paused selection when none plays, and use the lowest tab ID to break fallback ties.
- [x] Send play/pause, next and previous only to the selected tab's main frame. Preserve that selection while paused so play resumes the same tab.
- [ ] Extend selection across companion connections so Chrome, Edge or separate profiles do not all execute the same command. Introduce optional source identifiers compatibly; define a deterministic fallback for older clients.
- [x] Handle tab closure, navigation, reload, discard and background-worker restart. Expose `selectedTabId` in extension runtime status; the popup track follows the selection.
- [ ] Show the selected source in companion diagnostics and handle disconnected clients across companion connections.
- [ ] Complete the live multiple-tab scenarios in [release QA](docs/RELEASE_QA.md) in Chrome, Edge and Firefox.

Edge progress: the [2026-10-12 local-extension retest](docs/LIVE_QA_2026-10-12.md) passed targeted commands, simultaneous playback, source switching, pause clearing, autoplay/seek timing and reload/navigation/closure fallback. Worker restart, discard and the other listed environments still need live checks; keep the full-matrix item open.

**Done when:** with two tabs and two browser connections, the displayed track and command target stay aligned; one click affects exactly one source; closing it selects a valid fallback without stale playback state. Cover these cases in automated tests and a live smoke check.

### Make connection status reflect accepted work

- [x] Distinguish socket open, token accepted, disconnected and token rejected in the extension. Use the existing `YTM_TEST_RESULT` response for authentication status, including after token changes.
- [ ] Distinguish Discord transport connectivity from accepted/rejected activity commands. Correlate replies with their request nonce and expose rejection or timeout in diagnostics.
- [ ] Exercise Discord restart, companion restart, browser background restart and Windows sleep/resume. Check state replay and deduplication together, including a paused track whose metadata does not change.
- [x] Retain bounded reconnect delays and ensure keepalives do not reset playback pause/idle timers.

**Done when:** a wrong token or rejected activity cannot show a successful state; reconnecting restores the correct eligible presence without manually reloading the page. Automated failure/recovery tests accompany the change; the live QA matrix records browser and Discord versions.

The existing 20-second keepalive follows Chrome's documented approach. Chrome supports this WebSocket lifetime behavior from version 116; declare and test the supported browser baseline rather than relying on an unstated minimum. See [Chrome WebSocket service worker guidance](https://developer.chrome.com/docs/extensions/how-to/web-platform/websockets).

## Milestone 2 Installation and setup that work on a clean PC

The prerequisite documentation and remaining translations can ship as a small patch before Milestone 1 is complete.

### Resolve runtime prerequisites

- [ ] Correct the framework-dependent installation instructions to account for both the Windows Desktop and ASP.NET Core dependencies.
- [ ] Test the current setup in a clean Windows VM with no .NET SDK and separately with only the documented prerequisites installed.
- [ ] Prefer the existing `-SelfContained` packaging option for the main end-user setup if its measured size and update behavior are acceptable. Otherwise add an explicit prerequisite check and actionable installation guidance.
- [ ] Keep exactly one unambiguous setup asset per release unless updater asset selection is changed deliberately. Verify the complete setup remains within the updater's existing size limit.

**Done when:** a clean machine can reach onboarding using the documented installation path; updating preserves settings/token; uninstall and portable-copy isolation still pass [release QA](docs/RELEASE_QA.md). Self-contained publishing includes the runtime, with a larger distribution size as the tradeoff: [Microsoft deployment guidance](https://learn.microsoft.com/en-us/dotnet/core/deploying/).

### Guide users to a working connection

- [x] Provide four setup steps with a Chrome/Edge store link, separate Firefox/developer instructions, manual token copying, save-and-check and opening/reloading YouTube Music. Show the companion version in onboarding and extension version/install type in popup/options.
- [x] Authenticate each new background socket before showing connected; share actionable missing/rejected/disconnected/no-track guidance between popup and options. Save settings centrally and distinguish save errors from connection failures.
- [ ] Extend companion diagnostics to show confirmed extension/track/Discord progress; do not equate the new extension connection badge with a Discord acknowledgement.
- [x] Preserve manual token copying and explain separate profile/install settings and reloading tabs after switching extensions.
- [ ] Add companion-side recovery guidance for an occupied port and unavailable Discord.
- [x] Move the remaining German-only onboarding feedback/error strings into `UiText` and localize new setup texts in German/English.
- [ ] Complete live setup acceptance in both supported languages, including failure paths; automated extension tests cover both translations.
- [ ] Review keyboard navigation, focus order and scaling in onboarding, settings and the mini player.

**Done when:** a new user can install both components and reach a working presence following the interface alone; every incomplete step explains the next action. Diagnostics exported during these scenarios contain no token.

## Milestone 3 Consistent releases and support

- [ ] Run extension behavior tests, archive checks and packaged-extension linting in the tag release workflow as well as PR CI; a tag must not bypass those checks.
- [ ] Add a compact live QA matrix for Chrome, Edge and Firefox: track change, pause/clear, ads, source switching, browser/Discord/companion restart and sleep/resume. Record versions, result and reproduction steps rather than assuming automation covers external applications.
- [ ] Include both component versions and the selected source in diagnostics. Document compatible app/extension combinations and test an older extension against a newer companion, since store and GitHub updates arrive separately.
- [ ] Extend the release checklist to record GitHub and store publication status separately, using the existing [store submission checklist](docs/STORE_SUBMISSION_CHECKLIST.md).
- [ ] Add a bug-report template requesting versions, browser, reproduction steps and an optional redacted diagnostics report.

**Done when:** each release has passing automated checks, a completed live matrix and an explicit store rollout status; mixed versions either work or explain an incompatibility clearly.

## Later work gated by demand

| Opportunity | Start when |
| --- | --- |
| Persistent Firefox distribution through a signed package/store listing | Firefox demand justifies maintaining a second store release path; the current README documents temporary loading. |
| Signed Windows binaries and publisher verification in the updater | A signing identity, key handling and renewal budget are available. Keep the existing checksum checks and test signature verification before enforcing it for updates. |
| Easier pairing without manual token transfer | Setup feedback shows token copying remains a major obstacle after guided onboarding. Evaluate a short-lived local pairing flow while preserving authentication. |
| Presence display presets, a quick privacy toggle or optional hotkeys | Concrete user requests establish which small feature is useful after reliability work. |
| ARM64 or additional desktop operating systems | There is actual demand and a test environment. Additional operating systems require work beyond packaging the current WPF app. |

Keep cloud accounts, listening-history analytics and a broad interface rewrite outside the current plan. Preserve the small, local companion model.

## Next implementation slice

The extension/tab-selection and guided-setup slices are implemented. Current automated setup checks pass **92 extension tests** and **208 Core tests**; build and packaging results and remaining live acceptance are recorded in [setup QA](docs/SETUP_QA_2026-10-12.md). Finish that acceptance before calling the onboarding slice release-ready.

The next implementation order is: **(1)** source selection across browser/companion connections, **(2)** confirmed Discord activity acknowledgements and actionable failures, **(3)** the remaining release test matrix, including clean-machine prerequisites. Publishing and a version bump are separate tasks; the current checkout remains 1.4.0.

Update this roadmap when a slice ships, move completed changes to the changelog, and reassess the next milestone using bugs and setup feedback. Each implementation should retain passing Core tests, version checks and relevant release checks; new cross-component behavior also needs the matching live QA scenario.
