using System.Globalization;

namespace YTMPresence.Core;

/// <summary>
/// Texte, die in der Discord-Presence erscheinen.
///
/// Anders als <c>UiText</c> im Tray-Projekt betrifft das nicht die eigene
/// Oberfläche, sondern das, was <b>andere Discord-Nutzer</b> zu sehen bekommen.
/// Diese Strings waren fest deutsch verdrahtet — ein englischer Nutzer zeigte
/// seinen Kontakten damit „Pausiert bei 1:23".
///
/// Maßgeblich ist die Sprache des Systems, auf dem der Companion läuft, analog
/// zur übrigen Oberfläche. Die Presence gehört zum Profil des Nutzers, deshalb
/// richtet sie sich nach seiner Spracheinstellung und nicht nach der seiner
/// Kontakte — die ist ohnehin nicht ermittelbar.
///
/// Discord begrenzt Button-Beschriftungen auf 32 Zeichen; die Übersetzungen
/// müssen darunter bleiben.
/// </summary>
public static class PresenceText
{
  private static string Lang =>
      CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();

  private static string T(string de, string en, string es)
  {
    return Lang switch
    {
      "de" => de,
      "es" => es,
      _ => en
    };
  }

  /// <summary>Kurztext am kleinen Symbol, während Musik läuft.</summary>
  public static string Playing => T(de: "Läuft", en: "Playing", es: "Reproduciendo");

  /// <summary>Kurztext am kleinen Symbol, während pausiert ist.</summary>
  public static string Paused => T(de: "Pausiert", en: "Paused", es: "En pausa");

  /// <summary>Statuszeile bei Pause mit bekannter Position.</summary>
  public static string PausedAt(string time, string artist) => T(
      de: $"Pausiert bei {time} · {artist}",
      en: $"Paused at {time} · {artist}",
      es: $"En pausa en {time} · {artist}");

  /// <summary>Statuszeile bei Pause ohne bekannte Position.</summary>
  public static string PausedWithoutTime(string details) => T(
      de: $"Pausiert · {details}",
      en: $"Paused · {details}",
      es: $"En pausa · {details}");

  /// <summary>Statuszeile, während Werbung läuft.</summary>
  public static string Advertisement => T(de: "Werbung", en: "Advertisement", es: "Anuncio");

  /// <summary>Beschriftung des Buttons zum aktuellen Track (max. 32 Zeichen).</summary>
  public static string OpenOnYouTubeMusic => T(
      de: "Auf YouTube Music öffnen",
      en: "Open on YouTube Music",
      es: "Abrir en YouTube Music");
}
