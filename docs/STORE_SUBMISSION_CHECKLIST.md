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
- Language: German, optionally English as a second listing
- Privacy policy: `docs/EXTENSION_PRIVACY.md` as a GitHub URL
- Support URL: `https://github.com/Kentarohakase/YTMPresence/issues`
- Homepage: `https://github.com/Kentarohakase/YTMPresence`

## Explaining the permissions

- `storage`: stores the token and companion URL locally.
- `https://music.youtube.com/*`: reads playback data only on YouTube Music and finds YouTube Music tabs for player commands.
- `localhost` and `127.0.0.1`: connects to the local companion only.
- The extension does not request `tabs`. Adding it would display Chrome's "Read your browsing history" warning at installation and require a new review.

## Data collection in the dashboard

Use answers consistent with the Firefox metadata (`browsingActivity`, `websiteContent` in `browser_specific_settings.gecko.data_collection_permissions`):

- Collected: website activity and website content (track metadata, the current YouTube Music URL).
- Transferred locally only, to `127.0.0.1`/`localhost`; no sharing with third parties, no sale, no creditworthiness or lending purposes.
- No analytics, tracking or advertising services.

## Store icon and screenshots

Prepare screenshots of:

- the extension popup with a connected companion
- the extension options with the connection test
- the mini player
- Discord Rich Presence
- the tray menu or diagnostics window

Requirements:

- Store icon (labelled „Händlersymbol" in the German interface): 128x128 PNG. `YTMPresence/extension/icons/icon128.png` meets the requirement; Google also recommends 16 px of transparent padding around a 96x96 graphic.
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

GitHub and the store are published separately; record both.

| Version | GitHub release | Chrome Web Store | Notes |
| --- | --- | --- | --- |
| 1.5.1 | published 2026-11-02 | not uploaded | Companion fix only; the extension is unchanged apart from its version number. Uploading it now would mean withdrawing the pending 1.5.0 review. |
| 1.5.0 | published 2026-11-02 | submitted 2026-11-02, in review | No permission changes since 1.4.0. The English listing was added with this upload. |
| 1.4.0 | published 2026-08-10 | published | Confirmed in the dashboard on 2026-11-02. |
| 1.3.1 | published 2026-08-09 | approved | First store submission. |
