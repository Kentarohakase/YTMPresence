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

  public static string DiscordRejected(string error) => T(
      de: $"Discord: abgelehnt · {error}",
      en: $"Discord: rejected · {error}",
      es: $"Discord: rechazado · {error}"
  );

  public static string DiscordNoReply => T(
      de: "Discord: keine Antwort",
      en: "Discord: no reply",
      es: "Discord: sin respuesta"
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
  public static string OnboardingIntro => T(de: "Der Companion läuft. Diese vier Schritte verbinden deine Browser-Extension mit ihm.", en: "The companion is running. Follow these four steps to connect your browser extension.", es: "The companion is running. Follow these four steps to connect your browser extension.");
  public static string OnboardingExtensionDataHeader => T(de: "Extension-Daten", en: "Extension data", es: "Extension data");
  public static string OnboardingInstructions => T(de: "In Chrome oder Edge die Erweiterungsverwaltung öffnen, Entwicklermodus aktivieren und „Entpackt laden“ wählen. Den Ordner YTMPresence/extension aus dem Repository auswählen. Andere YTMPresence-Versionen deaktivieren, dann mit Schritt 2 fortfahren. Vorhandene YouTube-Music-Tabs neu laden.", en: "In Chrome or Edge, open extension management, enable developer mode and choose “Load unpacked”. Select the repository’s YTMPresence/extension folder. Disable other YTMPresence versions, then continue with step 2. Reload existing YouTube Music tabs.", es: "In Chrome or Edge, open extension management, enable developer mode and choose “Load unpacked”. Select the repository’s YTMPresence/extension folder. Disable other YTMPresence versions, then continue with step 2. Reload existing YouTube Music tabs.");
  public static string ButtonLater => T(de: "Später", en: "Later", es: "Later");
  public static string ButtonDone => T(de: "Fertig", en: "Done", es: "Done");
  public static string ButtonSettingsShort => T(de: "Einstellungen", en: "Settings", es: "Settings");
  public static string ButtonYouTubeMusic => T(de: "YouTube Music", en: "YouTube Music", es: "YouTube Music");

  public static string OnboardingStepInstall => T(de: "1. Extension installieren", en: "1. Install the extension", es: "1. Install the extension");

  public static string OnboardingInstallHint => T(de: "Für Chrome und Edge ist die Extension im Chrome Web Store verfügbar. Für Firefox gilt die verlinkte Anleitung.", en: "For Chrome and Edge, the extension is available in the Chrome Web Store. For Firefox, follow the linked instructions.", es: "For Chrome and Edge, the extension is available in the Chrome Web Store. For Firefox, follow the linked instructions.");

  public static string OnboardingStoreButton => T(de: "Chrome / Edge: Web Store", en: "Chrome / Edge: Web Store", es: "Chrome / Edge: Web Store");

  public static string OnboardingFirefoxButton => T(de: "Firefox-Anleitung", en: "Firefox instructions", es: "Firefox instructions");

  public static string OnboardingStepToken => T(de: "2. Token kopieren", en: "2. Copy the token", es: "2. Copy the token");

  public static string OnboardingCopyTokenButton => T(de: "Token kopieren", en: "Copy token", es: "Copy token");

  public static string OnboardingCopyUrlButton => T(de: "Companion-URL kopieren", en: "Copy companion URL", es: "Copy companion URL");

  public static string OnboardingStepCheck => T(de: "3. In den Optionen speichern und prüfen", en: "3. Save and check in options", es: "3. Save and check in options");

  public static string OnboardingCheckHint => T(de: "Im Browser die Optionen der YTMPresence-Extension öffnen, Token einfügen und „Speichern und prüfen“ wählen. Erst „Companion verbunden“ bestätigt die Verbindung.", en: "In the browser, open the YTMPresence extension options, paste the token and choose “Save and check”. Only “Companion connected” confirms the connection.", es: "In the browser, open the YTMPresence extension options, paste the token and choose “Save and check”. Only “Companion connected” confirms the connection.");

  public static string OnboardingScopeHint => T(de: "Jedes Browserprofil und jede Store- oder lokale Installation hat eigene Einstellungen. Nur eine YTMPresence-Extension je Profil aktivieren. Nach einem Extension-Wechsel vorhandene YouTube-Music-Tabs neu laden.", en: "Each browser profile and each store or local installation has its own settings. Enable only one YTMPresence extension per profile. Reload existing YouTube Music tabs after switching extensions.", es: "Each browser profile and each store or local installation has its own settings. Enable only one YTMPresence extension per profile. Reload existing YouTube Music tabs after switching extensions.");

  public static string OnboardingAdvanced => T(de: "Erweiterte Einstellungen: Companion-URL", en: "Advanced settings: companion URL", es: "Advanced settings: companion URL");

  public static string OnboardingUrlHint => T(de: "Nur bei einer abweichenden Adresse: Diese URL in den erweiterten Extension-Einstellungen speichern und prüfen.", en: "Only when using a different address: save and check this URL in the extension’s advanced settings.", es: "Only when using a different address: save and check this URL in the extension’s advanced settings.");

  public static string OnboardingStepMusic => T(de: "4. YouTube Music öffnen", en: "4. Open YouTube Music", es: "4. Open YouTube Music");

  public static string OnboardingMusicHint => T(de: "Vorhandene Tabs nach einem Extension-Wechsel neu laden, dann einen Titel starten. Eine bestätigte Companion-Verbindung bestätigt noch keine Discord-Presence: Die Anzeige bitte in Discord prüfen. Du kannst die Einrichtung mit „Fertig“ selbst abschließen oder mit „Später“ verlassen.", en: "Reload existing tabs after switching extensions, then start a track. A confirmed companion connection does not confirm Discord presence: check the display in Discord. You can manually complete setup with “Done” or leave with “Later”.", es: "Reload existing tabs after switching extensions, then start a track. A confirmed companion connection does not confirm Discord presence: check the display in Discord. You can manually complete setup with “Done” or leave with “Later”.");

  public static string OnboardingDeveloper => T(de: "Für Entwickler: entpackte Extension", en: "For developers: unpacked extension", es: "For developers: unpacked extension");

  public static string OnboardingTokenCopied => T(de: "Token kopiert. Jetzt in den Extension-Optionen einfügen und speichern und prüfen.", en: "Token copied. Paste it in the extension options, then save and check.", es: "Token copied. Paste it in the extension options, then save and check.");

  public static string OnboardingUrlCopied => T(de: "URL kopiert. Bei Bedarf in den erweiterten Extension-Einstellungen einfügen.", en: "URL copied. If needed, paste it in the extension’s advanced settings.", es: "URL copied. If needed, paste it in the extension’s advanced settings.");

  public static string OnboardingCopyFailed => T(de: "Kopieren fehlgeschlagen. Den Wert im Textfeld markieren und mit Strg+C kopieren.", en: "Copy failed. Select the value in the text field and press Ctrl+C.", es: "Copy failed. Select the value in the text field and press Ctrl+C.");

  public static string OnboardingSaveFailed => T(de: "Abschluss konnte nicht gespeichert werden. Erneut „Fertig“ wählen oder mit „Später“ schließen.", en: "Could not save completion. Choose “Done” again or close with “Later”.", es: "Could not save completion. Choose “Done” again or close with “Later”.");

  public static string OnboardingOpenLinkFailed => T(de: "Link konnte nicht geöffnet werden. Die entsprechende Anleitung oder YouTube Music manuell im Browser öffnen.", en: "Could not open the link. Open the corresponding instructions or YouTube Music manually in your browser.", es: "Could not open the link. Open the corresponding instructions or YouTube Music manually in your browser.");

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
  public static string SettingsTokenCopied => T(de: "Token kopiert.", en: "Token copied.", es: "Token copied.");
  public static string SettingsTokenCopyFailed => T(de: "Token konnte nicht kopiert werden.", en: "The token could not be copied.", es: "The token could not be copied.");
  public static string SettingsTokenGenerated => T(de: "Neuer Token erzeugt. Speichern nicht vergessen.", en: "New token generated. Remember to save.", es: "New token generated. Remember to save.");
  public static string SettingsHostEmpty => T(de: "Host darf nicht leer sein.", en: "Host must not be empty.", es: "Host must not be empty.");
  public static string SettingsHostNotUrl => T(de: "Host darf nur Hostname oder IP enthalten, keine URL.", en: "Host may only contain a host name or IP address, not a URL.", es: "Host may only contain a host name or IP address, not a URL.");
  public static string SettingsWebSocketPathEmpty => T(de: "WebSocket-Pfad darf nicht leer sein.", en: "WebSocket path must not be empty.", es: "WebSocket path must not be empty.");
  public static string SettingsWebSocketPathInvalid => T(de: "WebSocket-Pfad darf keine Leerzeichen, Query oder Fragment enthalten.", en: "WebSocket path must not contain spaces, a query or a fragment.", es: "WebSocket path must not contain spaces, a query or a fragment.");
  public static string SettingsClientIdEmpty => T(de: "Discord Client ID darf nicht leer sein.", en: "Discord client ID must not be empty.", es: "Discord client ID must not be empty.");
  public static string SettingsTokenEmpty => T(de: "Security Token darf nicht leer sein.", en: "Security token must not be empty.", es: "Security token must not be empty.");
  public static string SettingsNotANumber(string label) => T(de: $"{label} muss eine Zahl sein.", en: $"{label} must be a number.", es: $"{label} must be a number.");
  public static string SettingsOutOfRange(string label, int min, int max) => T(de: $"{label} muss zwischen {min} und {max} liegen.", en: $"{label} must be between {min} and {max}.", es: $"{label} must be between {min} and {max}.");

  // PlayerWindow
  public static string PlayerWindowTitle => T(de: "YTM Presence Player", en: "YTM Presence player", es: "YTM Presence player");
  public static string PlayerTopMost => T(de: "Top", en: "Top", es: "Top");
  public static string TooltipPrevious => T(de: "Zurück", en: "Previous", es: "Previous");

  public static string OpenYouTubeMusicFailed => T(
      de: "YouTube Music konnte nicht geöffnet werden.",
      en: "YouTube Music could not be opened.",
      es: "YouTube Music could not be opened.");

  // --- Diagnosefenster ---------------------------------------------------

  public static string DiagUnknown => T(de: "Unbekannt", en: "Unknown", es: "Unknown");
  public static string DiagNoServerData => T(de: "Keine Serverdaten", en: "No server data", es: "No server data");
  public static string DiagServerRunning => T(de: "Läuft", en: "Running", es: "Running");
  public static string DiagServerStopped => T(de: "Gestoppt", en: "Stopped", es: "Stopped");
  public static string DiagNever => T(de: "nie", en: "never", es: "never");
  public static string DiagJustNow => T(de: "gerade eben", en: "just now", es: "just now");
  public static string DiagSecurityOk => T(de: "OK", en: "OK", es: "OK");

  public static string DiagExtensionConnected(int clients, string age) => T(
      de: $"Verbunden ({clients}), letzte Daten: {age}",
      en: $"Connected ({clients}), last data: {age}",
      es: $"Connected ({clients}), last data: {age}");

  public static string DiagSelectedSource(string source) => T(
      de: $" · Quelle: {source}",
      en: $" · source: {source}",
      es: $" · source: {source}");

  public static string DiagExtensionDisconnected(string age) => T(
      de: $"Nicht verbunden, letzte Daten: {age}",
      en: $"Not connected, last data: {age}",
      es: $"Not connected, last data: {age}");

  public static string DiagDiscordOk(string age) => T(
      de: $"OK, letzter Erfolg: {age}",
      en: $"OK, last success: {age}",
      es: $"OK, last success: {age}");

  public static string DiagDiscordError(string message) => T(
      de: $"Fehler: {message}",
      en: $"Error: {message}",
      es: $"Error: {message}");

  public static string DiagDiscordRejected(string message) => T(
      de: $"Abgelehnt: {message}",
      en: $"Rejected: {message}",
      es: $"Rejected: {message}");

  public static string DiagDiscordNoReply => T(
      de: "Keine Antwort von Discord",
      en: "No reply from Discord",
      es: "No reply from Discord");

  public static string DiagInvalidTokens(int count, string age) => T(
      de: $"{count} ungültige Token, zuletzt: {age}",
      en: $"{count} invalid tokens, last: {age}",
      es: $"{count} invalid tokens, last: {age}");

  public static string DiagUpdateCheckNever => T(
      de: "Noch nie erfolgreich durchgelaufen",
      en: "Has never completed successfully",
      es: "Has never completed successfully");

  public static string DiagUpdateCheckLastSuccess(string age) => T(
      de: $"Zuletzt erfolgreich: vor {age}",
      en: $"Last successful: {age} ago",
      es: $"Last successful: {age} ago");

  public static string DiagUpdateCheckStale => T(
      de: " – seit über zwei Wochen kein erfolgreicher Check",
      en: " – no successful check for over two weeks",
      es: " – no successful check for over two weeks");

  public static string DiagUpdateCheckDisabled => T(
      de: " (Prüfung beim Start ist deaktiviert)",
      en: " (check on startup is disabled)",
      es: " (check on startup is disabled)");

  public static string DiagWindowTitle => T(de: "YTM Presence Diagnose", en: "YTM Presence diagnostics", es: "YTM Presence diagnostics");
  public static string DiagHeading => T(de: "Diagnose", en: "Diagnostics", es: "Diagnostics");
  public static string DiagLabelProgress => T(de: "Fortschritt", en: "Progress", es: "Progress");
  public static string DiagLabelSecurity => T(de: "Sicherheit", en: "Security", es: "Security");
  public static string DiagLabelSettings => T(de: "Einstellungen", en: "Settings", es: "Settings");
  public static string DiagLabelUpdateCheck => T(de: "Update-Check", en: "Update check", es: "Update check");
  public static string DiagLabelUpdated => T(de: "Aktualisiert", en: "Updated", es: "Updated");
  public static string DiagButtonCreateReport => T(de: "Bericht erstellen", en: "Create report", es: "Create report");

  public static string DiagCopied => T(de: "Diagnose kopiert.", en: "Diagnostics copied.", es: "Diagnostics copied.");
  public static string DiagCopyFailed => T(de: "Kopieren fehlgeschlagen.", en: "Copying failed.", es: "Copying failed.");

  public static string DiagReportCreated(string path) => T(
      de: $"Bericht erstellt: {path}",
      en: $"Report created: {path}",
      es: $"Report created: {path}");

  public static string DiagReportFailed(string message) => T(
      de: $"Bericht fehlgeschlagen: {message}",
      en: $"Report failed: {message}",
      es: $"Report failed: {message}");

  // --- Mini-Player -------------------------------------------------------

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
  public static string PlayerNoExtension => T(de: "Keine Extension verbunden", en: "No extension connected", es: "No extension connected");
  public static string PlayerCommandSent(string label) => T(de: $"{label} gesendet", en: $"{label} sent", es: $"{label} sent");
  public static string PlayerCommandFailed(string label) => T(de: $"{label} fehlgeschlagen", en: $"{label} failed", es: $"{label} failed");

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
