# Store submission checklist

This checklist covers the Chrome Web Store, Microsoft Edge Add-ons and compatible Chromium stores.

## Before the upload

- Check the manifest version in `YTMPresence/extension/manifest.json`.
- Build the release: `.\scripts\package-release.ps1`
- Inspect the extension ZIP: `artifacts\release\YTMPresence-extension.zip`
- Use this privacy URL: `https://github.com/Kentarohakase/YTMPresence/blob/main/docs/EXTENSION_PRIVACY.md`
- Use the store text in [CHROME_WEB_STORE.md](CHROME_WEB_STORE.md).
- Check the extension ZIP for local tokens, logs or build artifacts; none should be included.

## Required information

- Name and short description are read from `_locales/<language>/messages.json` (`appName`, `appDescription`). They cannot be edited in the dashboard; changes require a new package.
- State **in the first paragraph** that the extension requires the companion app, so users see the requirement before installing.
- Category: Music or Productivity
- Languages: German and English; the English listing was added to the recorded 1.5.0 submission on 2026-11-02.
- Privacy policy: `docs/EXTENSION_PRIVACY.md` as a GitHub URL
- Support URL: `https://github.com/Kentarohakase/YTMPresence/issues`
- Homepage: `https://github.com/Kentarohakase/YTMPresence`

## Explaining the permissions

- `storage`: stores the token and companion URL locally.
- `https://music.youtube.com/*`: reads playback data only on YouTube Music and finds YouTube Music tabs for player commands.
- `localhost` and `127.0.0.1`: connects to the local companion only.
- The extension does not request `tabs`. Adding it would display Chrome's "Read your browsing history" warning at installation and require a new review.

## Data collection in the dashboard

Describe the full data flow, including the companion's transfer to Discord. Keep the answers consistent with the privacy notice and Firefox metadata (`browsingActivity`, `websiteContent` in `browser_specific_settings.gecko.data_collection_permissions`):

- Collected: website activity and website content (track metadata, the current YouTube Music URL).
- The extension sends playback data only to the local companion at `127.0.0.1`/`localhost`, authenticated with the companion token.
- The companion passes Rich Presence information to the local Discord desktop app. Discord receives track title, artist, album, cover URL, playback state, timing and links, depending on settings and available metadata, and may display them to other users according to Discord's activity-sharing settings. This is a transfer to a third party outside the device.
- The mini player can fetch cover images from the supplied image URL; the companion also contacts GitHub for update checks and downloads. See [the privacy notice](EXTENSION_PRIVACY.md#data-sharing).
- No sale of data or use for creditworthiness or lending purposes. YTMPresence has no analytics, tracking or advertising integration and sends no data to developer-operated collection servers.

Review the dashboard declarations against this flow before submitting. The public listing retrieved during the 2026-11-07 check said that the developer does not collect or use data; that snapshot does not verify the current dashboard answers. Google's [user-data FAQ](https://developer.chrome.com/docs/webstore/program-policies/user-data-faq) requires disclosure even for local data processing. Do not use a blanket "no sharing with third parties" statement for the complete extension-and-companion workflow.

## Store icon and screenshots

Prepare screenshots of:

- the extension popup with a connected companion
- the extension options with the connection test
- the mini player
- Discord Rich Presence
- the tray menu or diagnostics window

Requirements:

- Store icon (labelled „Händlersymbol" in the German interface): 128x128 PNG. `YTMPresence/extension/icons/icon128.png` meets the requirement and keeps the 16 px of transparent padding around a 96x96 graphic that Google recommends. All icons are generated from `branding/ytmpresence-logo.svg` with `scripts/render-brand-icons.ps1`.
- Screenshots: 1280x800 or 640x400, at least one, at most five. Show Discord presence in the first image, which appears as the search-result preview.
- Before uploading, check that the security token is not legible in any image.

## After the upload

- Wait for the store review.
- Add the store URL to the README and `docs/CHROME_WEB_STORE.md`. (Chrome: done with 1.3.1)
- Check that the store version matches the GitHub release version.

## For every further version

Store updates require a separate upload; a GitHub release alone only reaches users who loaded the extension unpacked.

- Upload the new package in the dashboard for store users to receive the update.
- Permission changes trigger another review and can delay approval.
- Name and short description come from `_locales/`; changing them also requires a new package.

## Release status

Checked on 2026-11-07 against the [GitHub releases API](https://api.github.com/repos/Kentarohakase/YTMPresence/releases?per_page=100) and the available [public store listing](https://chromewebstore.google.com/detail/ytm-discord-presence-brid/obdinnbalnbmpkcaopnhnkdgdkcgligi). GitHub confirms 1.5.1 as the latest published release; 1.5.0 and 1.5.1 are neither drafts nor prereleases. A later check of the releases API on 2026-11-15 lists 1.6.0 as the latest release, published 2026-11-14 23:00 UTC (2026-11-15 in Vienna), neither draft nor prerelease. The store was not rechecked then. A further check on 2026-11-15 lists 1.6.1 as the latest release, published 2026-11-14 23:59 UTC (2026-11-15 in Vienna), neither draft nor prerelease; its four artifacts match the published `SHA256SUMS.txt`. Another check on 2026-11-15 lists 1.6.2 as the latest release, published 2026-11-15 09:28 UTC (10:28 in Vienna), neither draft nor prerelease, with five assets; its checksums were not compared separately. A check later that day lists 1.6.3 as the latest release, published 2026-11-15 10:03 UTC (11:03 in Vienna), neither draft nor prerelease, with five assets; its checksums were not compared separately either. The last check on 2026-11-15 lists 1.6.4 as the latest release, published 2026-11-15 16:03 UTC (17:03 in Vienna), neither draft nor prerelease, with five assets; its checksums were not compared separately.

The retrieved store page reports 1.4.0, updated 2026-08-10, but comes from a cached crawl. A live store check was unavailable, and the developer dashboard was not inspected. The submission and review entries below preserve the repository's 2026-11-02 record; they are not a fresh confirmation of today's status. On 2026-11-12 the maintainer reported that 1.5.0 is live in the store; that is the maintainer's statement, not an independent check.

"Submitted" means sent for review, "in review" records a pending review, and "published" means available to store users. Approval alone does not establish publication. GitHub publication is independent of all three store states.

| Version | GitHub release | Store submission | Store review | Store publication | Notes |
| --- | --- | --- | --- | --- | --- |
| 1.6.4 | [Published 2026-11-15](https://github.com/Kentarohakase/YTMPresence/releases/tag/v1.6.4) | Not uploaded as of 2026-11-15 | Not recorded | Not published | Extension change: German texts address the user consistently with "du". No permission changes. |
| 1.6.3 | [Published 2026-11-15](https://github.com/Kentarohakase/YTMPresence/releases/tag/v1.6.3) | Not uploaded as of 2026-11-15 | Not recorded | Not published | Extension identical to 1.6.2 apart from its version number. No permission changes. |
| 1.6.2 | [Published 2026-11-15](https://github.com/Kentarohakase/YTMPresence/releases/tag/v1.6.2) | Not uploaded as of 2026-11-15 | Not recorded | Not published | Extension change: new icon set only. No permission changes. |
| 1.6.1 | [Published 2026-11-15](https://github.com/Kentarohakase/YTMPresence/releases/tag/v1.6.1) | Not uploaded as of 2026-11-15 | Not recorded | Not published | Extension changes: **Copy diagnostics** in the popup, the notice about an outdated companion, the welcome page on first install. No permission changes. Uploading it while 1.6.0 is in review would withdraw that review. |
| 1.6.0 | [Published 2026-11-15](https://github.com/Kentarohakase/YTMPresence/releases/tag/v1.6.0) | Submitted 2026-11-15, as reported by the maintainer | In review on 2026-11-15; outcome not recorded | Not confirmed | Extension changes: complete cover URLs and the version in the connection test. No permission changes. |
| 1.5.1 | [Published 2026-11-02](https://github.com/Kentarohakase/YTMPresence/releases/tag/v1.5.1) | Not uploaded in the 2026-11-02 record; current status unverified | Not recorded | Unverified | Companion fix only; the extension differs only in version number. The 2026-11-02 note warned that uploading it would withdraw the pending 1.5.0 review. |
| 1.5.0 | [Published 2026-11-02](https://github.com/Kentarohakase/YTMPresence/releases/tag/v1.5.0) | Submitted 2026-11-02, per repository record | In review on 2026-11-02; outcome not recorded | Live, reported by the maintainer on 2026-11-12 | No permission changes since 1.4.0. The English listing was added to this submission. |
| 1.4.0 | [Published 2026-08-10](https://github.com/Kentarohakase/YTMPresence/releases/tag/v1.4.0) | Date not recorded | Not separately recorded | Recorded as published in the dashboard on 2026-11-02; also shown by the cached public page | The cached listing does not establish the current latest store version. |
| 1.3.1 | [Published 2026-08-09](https://github.com/Kentarohakase/YTMPresence/releases/tag/v1.3.1) | First submission; date not recorded | Approved, per historical record | Not independently verified | First approved store submission. |
