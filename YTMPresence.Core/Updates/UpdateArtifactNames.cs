namespace YTMPresence.Core.Updates;

/// <summary>
/// Namen der Release-Artefakte, an denen sich Update-Check und Download
/// orientieren. Bewusst eine gemeinsame Quelle: Auswahl (UpdateChecker) und
/// Verifikation (UpdateDownloadService) müssen exakt dieselbe Zeichenkette und
/// dieselbe Vergleichsart verwenden, sonst wird ein Asset ausgewählt, das die
/// anschließende Prüfung wieder ablehnt.
/// </summary>
public static class UpdateArtifactNames
{
  /// <summary>Dateiname der Prüfsummenliste, exakt wie von package-release.ps1 erzeugt.</summary>
  public const string ChecksumsFileName = "SHA256SUMS.txt";

  /// <summary>Endung, an der das Setup-Asset erkannt wird.</summary>
  public const string SetupFileNameSuffix = "-setup.exe";
}
