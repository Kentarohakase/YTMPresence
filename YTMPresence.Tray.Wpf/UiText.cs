using System.Globalization;

namespace YTMPresence.TrayWpf;

internal static class UiText
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
