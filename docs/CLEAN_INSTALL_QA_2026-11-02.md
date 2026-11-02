# Clean install QA — 2026-11-02

Scope: the self-contained setup from pull request #2 on a Windows installation without .NET. This is not a full release sign-off; see [release QA](RELEASE_QA.md) for the complete list.

## Environment and setups

- Windows Sandbox on Windows 11 Pro, guest `Microsoft Windows NT 10.0.26100.0`, networking disabled. The sandbox contains no .NET runtime; the script checks `%ProgramFiles%\dotnet\shared` before it starts.
- New setup: self-contained build of `2289086` (*Ship self-contained releases and check them before publishing*), 61,780,355 bytes. Its version still reads 1.4.0 because the bump to 1.5.0 is a separate step.
- Previous setup: local build of the tag `v1.4.0`, framework-dependent, 2,240,867 bytes. The published 1.4.0 setup has 2,240,851 bytes; it was not downloaded.
- Command, run from the repository root:

  ```powershell
  .\scripts\test-clean-install.ps1 -SetupPath artifacts\release\YTMPresence-1.4.0-win-x64-setup.exe -PreviousSetupPath artifacts\previous\YTMPresence-1.4.0-win-x64-setup.exe
  ```

## Results

| Check | Fresh install | Update from 1.4.0 |
| --- | --- | --- |
| Setup exits with code 0 | Pass | Pass (both setups) |
| App installed, `coreclr.dll` in the app folder | Pass | Pass |
| Companion listens on `127.0.0.1:17373` | Pass | Pass |
| Process still running, log written | Pass | Pass |
| Settings with token created / kept | Pass, created | Pass, token hash unchanged |
| Uninstaller stops the running companion | Pass | Pass |
| App folder, start menu entry and uninstall entry removed | Pass | Pass |
| Settings left in `%APPDATA%` after uninstall | Kept (by design) | Kept (by design) |

All 24 checks passed; three further lines are informational. The framework-dependent 1.4.0 did not start in the same sandbox, which reproduces the original problem.

## Observations

- The app log shows `Shutdown was requested by the installer` before each uninstall: the installer reached the app through its path-bound shutdown event, not through the Restart Manager fallback.
- Without network the update check fails as expected (`api.github.com` unknown) and the app keeps running.
- Two kinds of German text remain: the logger's first line (`Logger initialisiert`) and the update-check error messages in `UpdateChecker.cs`, which also reach the interface.

## Not covered

- The getting-started window and its content, the extension connection and Discord. These need the manual checks in [release QA](RELEASE_QA.md).
- The in-app update download. The update round runs the new setup over the old installation silently, which is what the updater does after its download and checksum check.
