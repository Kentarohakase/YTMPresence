namespace YTMPresence.Core.Updates;

/// <summary>
/// Größenlimits für alle Update-Downloads. Die Limits werden vor dem Download
/// über Content-Length geprüft (sofern vorhanden) und beim Streaming unabhängig
/// davon erzwungen.
///
/// Begründung der Werte: Das aktuelle Setup-EXE liegt (auch self-contained)
/// deutlich unter 150 MB, SHA256SUMS.txt enthält wenige Zeilen und die
/// GitHub-Release-Metadaten wenige hundert KB. Die Limits sind großzügige
/// Obergrenzen gegen fehlerhafte oder böswillig übergroße Antworten.
/// </summary>
public sealed record DownloadSizePolicy
{
  public static DownloadSizePolicy Default { get; } = new();

  /// <summary>Maximalgröße des Setup-EXE (Standard: 300 MB).</summary>
  public long MaxSetupBytes { get; init; } = 300L * 1024 * 1024;

  /// <summary>Maximalgröße der SHA256SUMS.txt (Standard: 64 KB).</summary>
  public long MaxChecksumsBytes { get; init; } = 64 * 1024;

  /// <summary>Maximalgröße der Release-Metadatenantwort (Standard: 4 MB).</summary>
  public long MaxMetadataBytes { get; init; } = 4L * 1024 * 1024;
}
