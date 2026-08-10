# Store submission checklist

This checklist covers the Chrome Web Store, Microsoft Edge Add-ons and compatible Chromium stores.

## Before the upload

- Check the manifest version in `YTMPresence/extension/manifest.json`.
- Build the release: `.\scripts\package-release.ps1`
- Inspect the extension ZIP: `artifacts\release\YTMPresence-extension.zip`
- Have the privacy URL ready: `https://github.com/Kentarohakase/YTMPresence/blob/main/docs/EXTENSION_PRIVACY.md`
- Take the store text from [CHROME_WEB_STORE.md](CHROME_WEB_STORE.md).
- Verify that no local tokens, logs or build artifacts ended up in the extension ZIP.

## Required information

- Name and short description come from `_locales/<language>/messages.json` (`appName`, `appDescription`) and are not maintained in the dashboard. Changing them requires a new package.
- The full description has to mention the companion app **in the first paragraph**. Without it the extension does nothing; users who only discover that after installing will rate accordingly.
- Category: Music or Productivity
- Language: German, optionally English as a second listing
- Privacy policy: `docs/EXTENSION_PRIVACY.md` as a GitHub URL
- Support URL: `https://github.com/Kentarohakase/YTMPresence/issues`
- Homepage: `https://github.com/Kentarohakase/YTMPresence`

## Explaining the permissions

- `storage`: stores the token and companion URL locally.
- `https://music.youtube.com/*`: reads playback data on YouTube Music only, and finds the tabs for player commands.
- `localhost` and `127.0.0.1`: connects to the local companion only.
- `tabs` is deliberately not requested. Should it ever be added back: Chrome then displays "Read your browsing history" at install time, and adding a permission after the fact triggers a fresh review.

## Data collection in the dashboard

The answers have to match the Firefox metadata (`browsingActivity`, `websiteContent` in `browser_specific_settings.gecko.data_collection_permissions`):

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

- Store icon (labelled „Händlersymbol" in the German interface, which is Google's rendering of "store icon"): 128x128 PNG. `YTMPresence/extension/icons/icon128.png` meets the requirement; Google additionally recommends 16 px of transparent padding around a 96x96 graphic.
- Screenshots: 1280x800 or 640x400, at least one, at most five. The first image appears as the preview in search results — that is where the Discord presence belongs, not the options page.
- Before uploading, check that the security token is not legible in any image.

## After the upload

- Wait for the store review.
- Add the store URL to the README and `docs/CHROME_WEB_STORE.md`. (Chrome: done with 1.3.1)
- Check that the store version matches the GitHub release version.

## For every further version

The store does not update itself. A GitHub release only reaches users who loaded the extension unpacked.

- Upload the new package in the dashboard, otherwise store users stay on the old version.
- Permission changes trigger another review and can delay approval.
- Name and short description come from `_locales/`; changing them also requires a new package.
