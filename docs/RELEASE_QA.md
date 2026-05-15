# Release-QA

Diese Checkliste ist für jeden Release-Tag gedacht, bevor das Release breit geteilt wird.

## Vor dem Tag

- `Directory.Build.props` und `YTMPresence/extension/manifest.json` haben dieselbe Version.
- `CHANGELOG.md` enthält einen Eintrag für diese Version.
- `dotnet build YTMPresence.slnx --configuration Release` läuft ohne Fehler.
- `.\scripts\package-release.ps1` erzeugt App, Bundle-ZIP, Extension-ZIP, Setup-EXE und `SHA256SUMS.txt`.
- `.\scripts\verify-release.ps1` meldet die erwartete Version und den erwarteten Runtime-Zieltyp.

## Release-Artefakte

- `YTMPresence-<version>-win-x64-setup.exe` startet ohne Adminrechte.
- `YTMPresence-<version>-win-x64.zip` enthält `app/`, `extension/` und `RELEASE.txt`.
- `YTMPresence-extension.zip` enthält Manifest, Scripts, Optionen, Popup und alle Icons.
- `SHA256SUMS.txt` enthält Bundle-ZIP, Extension-ZIP und Setup-EXE.

## Installations-Test

- Setup über eine vorhandene Installation ausführen.
- YTMPresence startet nach der Installation.
- Startmenü-Verknüpfung startet die App.
- Windows zeigt YTMPresence unter den installierten Apps.
- Deinstallation entfernt Startmenü-Eintrag, Autostart-Wert, App-Ordner und Windows-Deinstallationseintrag.

## Clean-Test

- Auf einem frischen Windows-Profil oder einer VM installieren.
- Erste-Schritte-Fenster erscheint beim ersten Start.
- Token und Companion-URL lassen sich kopieren.
- Extension verbindet sich nach Speichern von Token und URL.
- Diagnosefenster zeigt Server, Extension und Discord sinnvoll an.

## Nach dem Push

- GitHub Actions Release-Workflow ist grün.
- GitHub Release ist nicht als Draft oder Prerelease markiert.
- Release Notes entsprechen dem Changelog-Eintrag.
- Assets sind im Release vorhanden und herunterladbar.
