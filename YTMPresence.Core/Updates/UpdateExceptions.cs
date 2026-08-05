namespace YTMPresence.Core.Updates;

/// <summary>
/// Sicherheitsverstoß im Update-Ablauf: unzulässige URL, fehlende oder
/// widersprüchliche Prüfsumme, fehlgeschlagene SHA256-Verifikation.
/// Ein Setup darf nach dieser Exception niemals gestartet werden.
/// </summary>
public class UpdateSecurityException : Exception
{
  public UpdateSecurityException(string message) : base(message) { }
  public UpdateSecurityException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Technischer Fehler beim Herunterladen oder Starten des Updates
/// (Netzwerk, Zeitüberschreitung, Größenlimit, unvollständiger Stream).
/// </summary>
public class UpdateDownloadException : Exception
{
  public UpdateDownloadException(string message) : base(message) { }
  public UpdateDownloadException(string message, Exception innerException) : base(message, innerException) { }
}
