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

- Name und Kurzbeschreibung stammen aus `_locales/<sprache>/messages.json` (`appName`, `appDescription`) und werden nicht im Dashboard gepflegt. Änderungen daran erfordern ein neues Paket.
- Die ausführliche Beschreibung muss die Companion-App **im ersten Absatz** nennen. Ohne sie ist die Extension funktionslos; Nutzer, die das erst nach der Installation merken, bewerten entsprechend.
- Kategorie: Musik oder Produktivität
- Sprache: Deutsch, optional Englisch
- Privacy Policy: `docs/EXTENSION_PRIVACY.md` als GitHub-URL
- Support-URL: `https://github.com/Kentarohakase/YTMPresence/issues`
- Homepage: `https://github.com/Kentarohakase/YTMPresence`

## Berechtigungen erklären

- `storage`: speichert Token und Companion-URL lokal.
- `https://music.youtube.com/*`: liest Playback-Daten nur auf YouTube Music und findet die Tabs für Player-Kommandos.
- `localhost` und `127.0.0.1`: verbindet sich nur mit dem lokalen Companion.
- `tabs` wird bewusst nicht angefordert. Falls es je wieder hinzukommt: Chrome zeigt dann bei der Installation „Deinen Browserverlauf lesen" an, und eine nachträgliche Berechtigungserweiterung löst ein neues Review aus.

## Datenerhebung im Dashboard

Die Angaben müssen zu den Firefox-Metadaten passen (`browsingActivity`, `websiteContent` in `browser_specific_settings.gecko.data_collection_permissions`):

- Erhoben werden Angaben zur Website-Aktivität und Website-Inhalte (Track-Metadaten, aktuelle YouTube-Music-URL).
- Übertragung ausschließlich lokal an `127.0.0.1`/`localhost`; keine Weitergabe an Dritte, kein Verkauf, keine Bonitäts- oder Kreditzwecke.
- Keine Analyse-, Tracking- oder Werbedienste.

## Screenshots

Erstelle vor dem Upload Screenshots von:

- Extension-Popup mit verbundenem Companion
- Extension-Optionen mit Verbindungstest
- Mini-Player
- Discord Rich Presence
- Tray-Menü oder Diagnosefenster

## Store-Symbol und Screenshots

- Store-Symbol („Händlersymbol" in der deutschen Oberfläche, gemeint ist das Store icon): 128x128 PNG. `YTMPresence/extension/icons/icon128.png` erfüllt die Anforderung; Google empfiehlt zusätzlich 16 px transparenten Rand um eine 96x96-Grafik.
- Screenshots: 1280x800 oder 640x400, mindestens einer, höchstens fünf. Das erste Bild erscheint als Vorschau in den Suchergebnissen — dort gehört die Discord-Presence hin, nicht die Optionsseite.
- Vor dem Hochladen prüfen, dass auf keinem Bild das Security Token lesbar ist.

## Nach dem Upload

- Store-Review abwarten.
- Store-URL in README und `docs/CHROME_WEB_STORE.md` ergänzen. (Chrome: erledigt mit 1.3.1)
- Prüfen, ob die Store-Version zur GitHub-Release-Version passt.

## Bei jeder weiteren Version

Der Store aktualisiert sich nicht von selbst. Ein GitHub-Release erreicht nur die Nutzer, die die Extension entpackt geladen haben.

- Neues Paket im Dashboard hochladen, sonst bleiben Store-Nutzer auf der alten Fassung.
- Änderungen an Berechtigungen lösen ein erneutes Review aus und können die Freigabe verzögern.
- Name und Kurzbeschreibung stammen aus `_locales/`; Änderungen daran brauchen ebenfalls ein neues Paket.
