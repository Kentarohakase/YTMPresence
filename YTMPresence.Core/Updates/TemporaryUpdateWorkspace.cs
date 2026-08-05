namespace YTMPresence.Core.Updates;

/// <summary>
/// Eindeutiges temporäres Arbeitsverzeichnis für genau einen Updateversuch.
/// Parallele Versuche erhalten getrennte Verzeichnisse und können sich nicht
/// gegenseitig überschreiben. <see cref="Dispose"/> räumt bestmöglich auf,
/// ohne einen ursprünglichen Fehler zu verdecken.
/// </summary>
public sealed class TemporaryUpdateWorkspace : IDisposable
{
  private TemporaryUpdateWorkspace(string directory)
  {
    Directory = directory;
  }

  public string Directory { get; }

  /// <summary>
  /// True, wenn die Dateien beim Dispose erhalten bleiben sollen
  /// (Download-only-Ergebnis oder gestartetes Setup).
  /// </summary>
  public bool KeepFiles { get; set; }

  public static string GetDefaultRoot()
    => Path.Combine(Path.GetTempPath(), "YTMPresence", "updates");

  public static TemporaryUpdateWorkspace Create(string? rootDirectory = null)
  {
    var root = Path.GetFullPath(string.IsNullOrWhiteSpace(rootDirectory) ? GetDefaultRoot() : rootDirectory);

    for (var attempt = 0; attempt < 5; attempt++)
    {
      var candidate = Path.Combine(root, Path.GetRandomFileName());
      if (System.IO.Directory.Exists(candidate) || File.Exists(candidate))
        continue;

      System.IO.Directory.CreateDirectory(candidate);
      return new TemporaryUpdateWorkspace(candidate);
    }

    throw new UpdateDownloadException("Es konnte kein eindeutiges temporäres Update-Verzeichnis erstellt werden.");
  }

  public void Dispose()
  {
    if (KeepFiles)
      return;

    try
    {
      if (System.IO.Directory.Exists(Directory))
        System.IO.Directory.Delete(Directory, recursive: true);
    }
    catch (Exception ex)
    {
      // Nur protokollieren: Cleanup darf den eigentlichen Fehler nicht verdecken.
      Logger.Warn($"Temporäres Update-Verzeichnis konnte nicht gelöscht werden: {Directory} ({ex.Message})");
    }
  }

  /// <summary>
  /// Entfernt verwaiste Update-Verzeichnisse früherer Läufe (z. B. nach einem
  /// Absturz oder einem gestarteten Setup). Bewusst altersbasiert, damit ein
  /// noch laufendes Setup und frische Download-only-Ergebnisse erhalten bleiben.
  /// </summary>
  public static void TryCleanupStale(string? rootDirectory = null, TimeSpan? maxAge = null)
  {
    var root = Path.GetFullPath(string.IsNullOrWhiteSpace(rootDirectory) ? GetDefaultRoot() : rootDirectory);
    var cutoff = DateTime.UtcNow - (maxAge ?? TimeSpan.FromDays(2));

    try
    {
      if (!System.IO.Directory.Exists(root))
        return;

      foreach (var directory in System.IO.Directory.GetDirectories(root))
      {
        try
        {
          if (System.IO.Directory.GetLastWriteTimeUtc(directory) > cutoff)
            continue;

          System.IO.Directory.Delete(directory, recursive: true);
        }
        catch (Exception ex)
        {
          Logger.Warn($"Verwaistes Update-Verzeichnis konnte nicht gelöscht werden: {directory} ({ex.Message})");
        }
      }
    }
    catch (Exception ex)
    {
      Logger.Warn($"Bereinigung alter Update-Verzeichnisse fehlgeschlagen: {ex.Message}");
    }
  }
}
