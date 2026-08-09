using System.Globalization;

namespace YTMPresence.TrayWpf;

public static class UiText
{
  private static string Lang =>
      CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();

  public static string AppName => T(
      de: "YTM Presence",
      en: "YTM Presence",
      es: "YTM Presence"
  );

  public static string AppVersion(string version) => T(
      de: $"Version: {version}",
      en: $"Version: {version}",
      es: $"Versión: {version}"
  );

  public static string AlreadyRunningTitle => T(
      de: "YTM Presence",
      en: "YTM Presence",
      es: "YTM Presence"
  );

  public static string AlreadyRunningMessage => T(
      de: "YTM Presence läuft bereits für diesen Benutzer.",
      en: "YTM Presence is already running for this user.",
      es: "YTM Presence ya se está ejecutando para este usuario."
  );

  public static string StartupErrorTitle => T(
      de: "YTM Presence",
      en: "YTM Presence",
      es: "YTM Presence"
  );

  public static string StartupErrorMessage => T(
      de: "Die App konnte nicht korrekt gestartet werden. Details findest du im Log.",
      en: "The app could not be started correctly. See the log for details.",
      es: "La aplicación no se pudo iniciar correctamente. Consulta el registro para ver los detalles."
  );

  public static string UnexpectedErrorTitle => T(
      de: "YTM Presence",
      en: "YTM Presence",
      es: "YTM Presence"
  );

  public static string UnexpectedErrorMessage => T(
      de: "Es ist ein Fehler aufgetreten. Details findest du im Log.",
      en: "An error occurred. See the log for details.",
      es: "Se produjo un error. Consulta el registro para ver los detalles."
  );

  public static string TokenRenewedTitle => T(
      de: "Token erneuert",
      en: "Token renewed",
      es: "Token renovado"
  );

  public static string TokenRenewedMessage => T(
      de: "Neuer Token wurde generiert und in die Zwischenablage kopiert.",
      en: "A new token was generated and copied to the clipboard.",
      es: "Se generó un nuevo token y se copió al portapapeles."
  );

  public static string TokenCopyTitle => T(
      de: "Token kopiert",
      en: "Token copied",
      es: "Token copiado"
  );

  public static string TokenCopyMessage => T(
      de: "Token wurde in die Zwischenablage kopiert.",
      en: "The token was copied to the clipboard.",
      es: "El token se copió al portapapeles."
  );

  public static string ErrorTitle => T(
      de: "Fehler",
      en: "Error",
      es: "Error"
  );

  public static string TokenRenewErrorMessage => T(
      de: "Token konnte nicht erneuert werden.",
      en: "The token could not be renewed.",
      es: "No se pudo renovar el token."
  );

  public static string ServerStarting => T(
      de: "Server: startet…",
      en: "Server: starting…",
      es: "Servidor: iniciando…"
  );

  public static string ServerStopped => T(
      de: "Server: gestoppt",
      en: "Server: stopped",
      es: "Servidor: detenido"
  );

  public static string ServerRunning(string host, int port, string path) => T(
      de: $"Server: läuft ({host}:{port}{path})",
      en: $"Server: running ({host}:{port}{path})",
      es: $"Servidor: activo ({host}:{port}{path})"
  );

  public static string ExtensionWaiting => T(
      de: "Extension: wartet…",
      en: "Extension: waiting…",
      es: "Extensión: esperando…"
  );

  public static string ExtensionConnected(int count, string age) => T(
      de: $"Extension: verbunden ({count}) · letzte Daten: {age}",
      en: $"Extension: connected ({count}) · last data: {age}",
      es: $"Extensión: conectada ({count}) · últimos datos: {age}"
  );

  public static string ExtensionDisconnected(string age) => T(
      de: $"Extension: nicht verbunden · letzte Daten: {age}",
      en: $"Extension: not connected · last data: {age}",
      es: $"Extensión: no conectada · últimos datos: {age}"
  );

  public static string DiscordUnknown => T(
      de: "Discord: unbekannt…",
      en: "Discord: unknown…",
      es: "Discord: desconocido…"
  );

  public static string DiscordOk(string? ageSuffix) => T(
      de: "Discord: ok" + ageSuffix,
      en: "Discord: ok" + ageSuffix,
      es: "Discord: ok" + ageSuffix
  );

  public static string DiscordError(string error) => T(
      de: $"Discord: Fehler · {error}",
      en: $"Discord: error · {error}",
      es: $"Discord: error · {error}"
  );

  public static string TrackNone => T(
      de: "Track: —",
      en: "Track: —",
      es: "Pista: —"
  );

  public static string TrackInfo(bool isPlaying, string value)
  {
    var icon = isPlaying ? "▶" : "⏸";
    return T(
        de: $"Track: {icon} {value}",
        en: $"Track: {icon} {value}",
        es: $"Pista: {icon} {value}"
    );
  }

  public static string SecurityNone => T(
      de: "Security: —",
      en: "Security: —",
      es: "Seguridad: —"
  );

  public static string SecurityOk => T(
      de: "Security: ok",
      en: "Security: ok",
      es: "Seguridad: ok"
  );

  public static string SecurityInvalid(int count, string? ageSuffix) => T(
      de: $"Security: {count} ungültige Token" + ageSuffix,
      en: $"Security: {count} invalid tokens" + ageSuffix,
      es: $"Seguridad: {count} tokens inválidos" + ageSuffix
  );

  public static string AutostartEnable => T(
      de: "Autostart aktivieren",
      en: "Enable autostart",
      es: "Activar inicio automático"
  );

  public static string OnlyShowWhenPlaying => T(
      de: "Presence nur wenn Musik läuft",
      en: "Presence only while music plays",
      es: "Presence solo cuando suena música"
  );

  public static string IgnoreAds => T(
      de: "Werbung ignorieren",
      en: "Ignore ads",
      es: "Ignorar anuncios"
  );

  public static string CopyToken => T(
      de: "Token kopieren",
      en: "Copy token",
      es: "Copiar token"
  );

  public static string GenerateNewToken => T(
      de: "Neuen Token generieren",
      en: "Generate new token",
      es: "Generar nuevo token"
  );

  public static string Settings => T(
      de: "Einstellungen…",
      en: "Settings…",
      es: "Configuración…"
  );

  public static string OpenPlayer => T(
      de: "Mini-Player öffnen",
      en: "Open mini player",
      es: "Abrir minirreproductor"
  );

  public static string Diagnostics => T(
      de: "Diagnose öffnen",
      en: "Open diagnostics",
      es: "Abrir diagnostico"
  );

  public static string CheckForUpdates => T(
      de: "Nach Updates suchen",
      en: "Check for updates",
      es: "Buscar actualizaciones"
  );

  public static string UpdateChecking => T(
      de: "Suche nach Updates…",
      en: "Checking for updates…",
      es: "Buscando actualizaciones…"
  );

  public static string UpdateAvailable(string version) => T(
      de: $"Update verfügbar: {version}",
      en: $"Update available: {version}",
      es: $"Actualización disponible: {version}"
  );

  public static string DownloadUpdate(string version) => T(
      de: $"Update herunterladen: {version}",
      en: $"Download update: {version}",
      es: $"Descargar actualización: {version}"
  );

  public static string UpdateAvailableMessage(string version) => T(
      de: $"YTMPresence {version} ist verfügbar. Du kannst das Update über das Tray-Menü installieren.",
      en: $"YTMPresence {version} is available. You can install the update from the tray menu.",
      es: $"YTMPresence {version} está disponible. Puedes instalar la actualización desde el menú de la bandeja."
  );

  public static string NoUpdateAvailable(string version) => T(
      de: $"Du verwendest bereits die aktuelle Version ({version}).",
      en: $"You are already using the latest version ({version}).",
      es: $"Ya estás usando la versión actual ({version})."
  );

  public static string UpdateCheckFailed(string message) => T(
      de: $"Update-Check fehlgeschlagen: {message}",
      en: $"Update check failed: {message}",
      es: $"La comprobación de actualizaciones falló: {message}"
  );

  public static string OpenLatestRelease => T(
      de: "Neuestes Release öffnen",
      en: "Open latest release",
      es: "Abrir última versión"
  );

  public static string OpenOnboarding => T(
      de: "Erste Schritte öffnen",
      en: "Open first steps",
      es: "Abrir primeros pasos"
  );

  public static string OpenYtm => T(
      de: "YouTube Music öffnen",
      en: "Open YouTube Music",
      es: "Abrir YouTube Music"
  );

  public static string OpenLog => T(
      de: "Log öffnen",
      en: "Open log",
      es: "Abrir registro"
  );

  public static string OpenLogFolder => T(
      de: "Log Ordner öffnen",
      en: "Open log folder",
      es: "Abrir carpeta de registros"
  );

  public static string Exit => T(
      de: "Beenden",
      en: "Exit",
      es: "Salir"
  );

  public static string TooltipActive => T(
      de: "aktiv",
      en: "active",
      es: "activo"
  );

  public static string TooltipWaiting => T(
      de: "wartet",
      en: "waiting",
      es: "esperando"
  );

  public static string JustNow => T(
      de: "gerade eben",
      en: "just now",
      es: "ahora mismo"
  );

  public static string SecondsAgo(int value) => T(
      de: $"{value}s",
      en: $"{value}s",
      es: $"{value}s"
  );

  public static string MinutesAgo(int value) => T(
      de: $"{value}m",
      en: $"{value}m",
      es: $"{value}m"
  );

  public static string HoursAgo(int value) => T(
      de: $"{value}h",
      en: $"{value}h",
      es: $"{value}h"
  );

  // --- Fenster-Texte (per x:Static aus den XAML-Fenstern referenziert) ---
  // es spiegelt bewusst den englischen Text; Zielsprachen für 1.0 sind DE/EN.

  // Gemeinsame Buttons/Labels
  public static string ButtonCopy => T(de: "Kopieren", en: "Copy", es: "Copy");
  public static string ButtonCancel => T(de: "Abbrechen", en: "Cancel", es: "Cancel");
  public static string ButtonSave => T(de: "Speichern", en: "Save", es: "Save");
  public static string LabelSecurityToken => T(de: "Security Token", en: "Security token", es: "Security token");
  public static string LabelCompanionUrl => T(de: "Companion URL", en: "Companion URL", es: "Companion URL");

  // OnboardingWindow
  public static string OnboardingWindowTitle => T(de: "YTM Presence Erste Schritte", en: "YTM Presence – Getting started", es: "YTM Presence – Getting started");
  public static string OnboardingHeading => T(de: "YTM Presence einrichten", en: "Set up YTM Presence", es: "Set up YTM Presence");
  public static string OnboardingIntro => T(de: "Companion läuft. Verbinde jetzt die Browser-Extension mit diesen Daten.", en: "The companion is running. Now connect the browser extension with this data.", es: "The companion is running. Now connect the browser extension with this data.");
  public static string OnboardingExtensionDataHeader => T(de: "Extension-Daten", en: "Extension data", es: "Extension data");
  public static string OnboardingInstructions => T(de: "In Chrome oder Edge: Erweiterungen öffnen, Entwicklermodus aktivieren, extension-Ordner laden, Token und URL in den Optionen speichern.", en: "In Chrome or Edge: open Extensions, enable developer mode, load the extension folder, then save the token and URL in the options.", es: "In Chrome or Edge: open Extensions, enable developer mode, load the extension folder, then save the token and URL in the options.");
  public static string ButtonLater => T(de: "Später", en: "Later", es: "Later");
  public static string ButtonDone => T(de: "Fertig", en: "Done", es: "Done");
  public static string ButtonSettingsShort => T(de: "Einstellungen", en: "Settings", es: "Settings");
  public static string ButtonYouTubeMusic => T(de: "YouTube Music", en: "YouTube Music", es: "YouTube Music");

  // SettingsWindow
  public static string SettingsWindowTitle => T(de: "YTM Presence Einstellungen", en: "YTM Presence settings", es: "YTM Presence settings");
  public static string GroupConnection => T(de: "Verbindung", en: "Connection", es: "Connection");
  public static string ButtonNew => T(de: "Neu", en: "New", es: "New");
  public static string LabelHost => T(de: "Host", en: "Host", es: "Host");
  public static string LabelPort => T(de: "Port", en: "Port", es: "Port");
  public static string LabelWebSocketPath => T(de: "WebSocket-Pfad", en: "WebSocket path", es: "WebSocket path");
  public static string GroupPresence => T(de: "Presence", en: "Presence", es: "Presence");
  public static string LabelDiscordUpdateInterval => T(de: "Discord Update-Intervall", en: "Discord update interval", es: "Discord update interval");
  public static string LabelIdleClearSeconds => T(de: "Idle-Clear nach Sekunden", en: "Idle clear after seconds", es: "Idle clear after seconds");
  public static string LabelPausedClearSeconds => T(de: "Pause-Clear nach Sekunden", en: "Paused clear after seconds", es: "Paused clear after seconds");
  public static string LabelAds => T(de: "Werbung", en: "Ads", es: "Ads");
  public static string AdShowAdvertisement => T(de: "Als Werbung anzeigen", en: "Show as ad", es: "Show as ad");
  public static string AdIgnore => T(de: "Ignorieren", en: "Ignore", es: "Ignore");
  public static string AdClear => T(de: "Presence löschen", en: "Clear presence", es: "Clear presence");
  public static string SettingsOnlyShowWhenPlaying => T(de: "Presence nur anzeigen, wenn Musik läuft", en: "Only show presence while music plays", es: "Only show presence while music plays");
  public static string GroupDiscord => T(de: "Discord", en: "Discord", es: "Discord");
  public static string LabelClientId => T(de: "Client ID", en: "Client ID", es: "Client ID");

  // PlayerWindow
  public static string PlayerWindowTitle => T(de: "YTM Presence Player", en: "YTM Presence player", es: "YTM Presence player");
  public static string PlayerTopMost => T(de: "Top", en: "Top", es: "Top");
  public static string TooltipPrevious => T(de: "Zurück", en: "Previous", es: "Previous");

  public static string PlayerStatePlaying => T(de: "Läuft", en: "Playing", es: "Playing");
  public static string PlayerStatePaused => T(de: "Pausiert", en: "Paused", es: "Paused");
  public static string PlayerNoTrack => T(de: "Kein Track", en: "No track", es: "No track");
  public static string PlayerWaitingForYtm => T(
      de: "Warte auf YouTube Music",
      en: "Waiting for YouTube Music",
      es: "Waiting for YouTube Music");
  public static string TooltipNext => T(de: "Weiter", en: "Next", es: "Next");
  public static string ButtonOpen => T(de: "Öffnen", en: "Open", es: "Open");
  public static string TooltipOpenTrack => T(de: "Track öffnen", en: "Open track", es: "Open track");

  // UpdateDownloadWindow
  public static string UpdateWindowTitle => T(de: "YTM Presence Update", en: "YTM Presence update", es: "YTM Presence update");
  public static string UpdateHeading => T(de: "Update installieren", en: "Install update", es: "Install update");
  public static string LabelSetupFile => T(de: "Setup-Datei", en: "Setup file", es: "Setup file");
  public static string LabelTarget => T(de: "Ziel", en: "Target", es: "Target");
  public static string ButtonOpenRelease => T(de: "Release öffnen", en: "Open release", es: "Open release");
  public static string ButtonDownloadOnly => T(de: "Nur herunterladen", en: "Download only", es: "Download only");
  public static string ButtonInstall => T(de: "Installieren", en: "Install", es: "Install");
  public static string ButtonClose => T(de: "Schließen", en: "Close", es: "Close");

  public static string UpdateSummary(string version) => T(
      de: $"YTMPresence {version} ist verfügbar. Das Setup wird heruntergeladen, per SHA256 verifiziert und erst nach erfolgreicher Prüfung gestartet.",
      en: $"YTMPresence {version} is available. The setup is downloaded, verified via SHA256, and only started after the check succeeds.",
      es: $"YTMPresence {version} is available. The setup is downloaded, verified via SHA256, and only started after the check succeeds.");

  public static string UpdateNoSetupAsset => T(
      de: "Dieses Release enthält kein Setup-Asset. Öffne stattdessen die Release-Seite.",
      en: "This release does not contain a setup asset. Open the release page instead.",
      es: "This release does not contain a setup asset. Open the release page instead.");

  public static string UpdateNoChecksumsAsset => T(
      de: "Dieses Release enthält keine SHA256SUMS.txt. Das Setup wird nicht heruntergeladen oder gestartet.",
      en: "This release has no SHA256SUMS.txt. The setup will not be downloaded or started.",
      es: "This release has no SHA256SUMS.txt. The setup will not be downloaded or started.");

  public static string UpdateStatusPreparing => T(
      de: "Download wird vorbereitet…",
      en: "Preparing download…",
      es: "Preparing download…");

  public static string UpdateStatusDownloadedOnly(string path) => T(
      de: $"Setup wurde heruntergeladen und die SHA256-Prüfsumme bestätigt: {path}",
      en: $"The setup was downloaded and its SHA256 checksum confirmed: {path}",
      es: $"The setup was downloaded and its SHA256 checksum confirmed: {path}");

  public static string UpdateStatusStartingSetup => T(
      de: "Prüfsumme bestätigt. Setup wird gestartet…",
      en: "Checksum confirmed. Starting setup…",
      es: "Checksum confirmed. Starting setup…");

  public static string UpdateStatusSetupStarted => T(
      de: "Setup wurde gestartet. YTM Presence beendet sich jetzt; folge den Installationsschritten.",
      en: "The setup was started. YTM Presence will now exit; follow the installation steps.",
      es: "The setup was started. YTM Presence will now exit; follow the installation steps.");

  public static string UpdateStatusCancelled => T(
      de: "Download abgebrochen. Temporäre Dateien wurden entfernt.",
      en: "Download cancelled. Temporary files were removed.",
      es: "Download cancelled. Temporary files were removed.");

  public static string UpdateStatusFailed(string message) => T(
      de: $"Update wurde nicht installiert: {message}",
      en: $"The update was not installed: {message}",
      es: $"The update was not installed: {message}");

  public static string UpdateOpenReleaseFailed => T(
      de: "Release-Seite konnte nicht geöffnet werden.",
      en: "The release page could not be opened.",
      es: "The release page could not be opened.");

  public static string UpdateProgressWithTotal(double percent, string done, string total) => T(
      de: $"{percent:0}% · {done} von {total}",
      en: $"{percent:0}% · {done} of {total}",
      es: $"{percent:0}% · {done} of {total}");

  public static string UpdateProgressPlain(string done) => T(
      de: $"{done} heruntergeladen",
      en: $"{done} downloaded",
      es: $"{done} downloaded");

  public static string UpdateProgressComplete(string size) => T(
      de: $"Download abgeschlossen: {size}",
      en: $"Download complete: {size}",
      es: $"Download complete: {size}");

  private static string T(string de, string en, string es)
  {
    return Lang switch
    {
      "de" => de,
      "es" => es,
      _ => en
    };
  }
}
