# Store-Submission-Checkliste

Diese Checkliste ist für Chrome Web Store, Microsoft Edge Add-ons und kompatible Chromium-Stores gedacht.

## Vor dem Upload

- Manifest-Version in `YTMPresence/extension/manifest.json` prüfen.
- Release bauen: `.\scripts\package-release.ps1`
- Extension-ZIP prüfen: `artifacts\release\YTMPresence-extension.zip`
- Privacy-URL bereithalten: `https://github.com/Kentarohakase/YTMPresence/blob/main/docs/EXTENSION_PRIVACY.md`
- Store-Text aus [CHROME_WEB_STORE.md](CHROME_WEB_STORE.md) übernehmen.
- Prüfen, dass keine lokalen Tokens, Logs oder Build-Artefakte im Extension-ZIP enthalten sind.

## Pflichtangaben

- Name: `YTM Discord Presence Bridge`
- Kurzbeschreibung: `Zeigt YouTube Music als Discord Rich Presence über einen lokalen Companion.`
- Kategorie: Musik oder Produktivität
- Sprache: Deutsch, optional Englisch
- Privacy Policy: `docs/EXTENSION_PRIVACY.md` als GitHub-URL
- Support-URL: `https://github.com/Kentarohakase/YTMPresence/issues`
- Homepage: `https://github.com/Kentarohakase/YTMPresence`

## Berechtigungen erklären

- `storage`: speichert Token und Companion-URL lokal.
- `tabs`: findet YouTube-Music-Tabs für Player-Kommandos.
- `https://music.youtube.com/*`: liest Playback-Daten nur auf YouTube Music.
- `localhost` und `127.0.0.1`: verbindet sich nur mit dem lokalen Companion.

## Screenshots

Erstelle vor dem Upload Screenshots von:

- Extension-Popup mit verbundenem Companion
- Extension-Optionen mit Verbindungstest
- Mini-Player
- Discord Rich Presence
- Tray-Menü oder Diagnosefenster

## Nach dem Upload

- Store-Review abwarten.
- Release-README aktualisieren, sobald eine Store-URL existiert.
- Store-URL in `docs/CHROME_WEB_STORE.md` und README ergänzen.
- Prüfen, ob die Store-Version zur GitHub-Release-Version passt.
