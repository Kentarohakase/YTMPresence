using System.Security.Cryptography;

namespace YTMPresence.Core.Updates;

/// <summary>
/// Ergebnis eines erfolgreich heruntergeladenen und SHA256-verifizierten Setups.
/// Vor dem Start muss <see cref="AcquireLaunchLockAsync"/> aufgerufen werden:
/// Es prüft Existenz, Dateinamen, Größe und Hash erneut und hält die Datei
/// anschließend mit einem Read-Share-Handle offen, sodass sie zwischen
/// erneuter Prüfung und Prozessstart nicht verändert werden kann.
/// </summary>
public sealed class VerifiedSetupPackage : IDisposable
{
  private readonly TemporaryUpdateWorkspace _workspace;
  private readonly long _maxAllowedBytes;

  public VerifiedSetupPackage(
    string filePath,
    string fileName,
    string sha256,
    long length,
    TemporaryUpdateWorkspace workspace,
    long maxAllowedBytes = 300L * 1024 * 1024)
  {
    FilePath = Path.GetFullPath(filePath);
    FileName = fileName;
    Sha256 = (sha256 ?? "").ToLowerInvariant();
    Length = length;
    _workspace = workspace;
    _maxAllowedBytes = maxAllowedBytes;
  }

  /// <summary>Absoluter Pfad des verifizierten Setups.</summary>
  public string FilePath { get; }

  /// <summary>Erwarteter Dateiname (exakt, wie in SHA256SUMS.txt).</summary>
  public string FileName { get; }

  /// <summary>Verifizierter SHA256-Hash (lowercase hex).</summary>
  public string Sha256 { get; }

  /// <summary>Dateigröße zum Zeitpunkt der Verifikation.</summary>
  public long Length { get; }

  public string WorkspaceDirectory => _workspace.Directory;

  /// <summary>Dateien beim Dispose behalten (Download-only oder gestartetes Setup).</summary>
  public bool KeepFilesOnDispose
  {
    get => _workspace.KeepFiles;
    set => _workspace.KeepFiles = value;
  }

  /// <summary>
  /// Wiederholt die Verifikation unmittelbar vor dem Start und sperrt die Datei
  /// gegen Schreib- und Löschzugriffe, bis das zurückgegebene Lock freigegeben wird.
  /// Lesen/Ausführen bleibt möglich (FileShare.Read), damit der Prozessstart klappt.
  /// </summary>
  public async Task<SetupLaunchLock> AcquireLaunchLockAsync(CancellationToken ct = default)
  {
    if (!ChecksumFileParser.IsValidSha256Hex(Sha256))
      throw new UpdateSecurityException("Setup-Start blockiert: Es liegt kein gültiger verifizierter SHA256-Hash vor.");

    if (Length <= 0 || _maxAllowedBytes <= 0 || Length > _maxAllowedBytes)
      throw new UpdateSecurityException("Setup-Start blockiert: Die verifizierte Dateigröße liegt außerhalb des erlaubten Limits.");

    if (!string.Equals(Path.GetFileName(FilePath), FileName, StringComparison.Ordinal))
      throw new UpdateSecurityException("Setup-Start blockiert: Der Dateiname entspricht nicht dem verifizierten Setup.");

    var expectedPath = Path.GetFullPath(Path.Combine(_workspace.Directory, FileName));
    if (!string.Equals(FilePath, expectedPath, StringComparison.OrdinalIgnoreCase))
      throw new UpdateSecurityException("Setup-Start blockiert: Die Datei liegt nicht im verifizierten Update-Arbeitsverzeichnis.");

    FileStream stream;
    try
    {
      stream = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
    }
    catch (FileNotFoundException ex)
    {
      throw new UpdateSecurityException("Setup-Start blockiert: Die verifizierte Setup-Datei existiert nicht mehr.", ex);
    }
    catch (DirectoryNotFoundException ex)
    {
      throw new UpdateSecurityException("Setup-Start blockiert: Die verifizierte Setup-Datei existiert nicht mehr.", ex);
    }

    try
    {
      if (stream.Length != Length)
        throw new UpdateSecurityException("Setup-Start blockiert: Die Dateigröße hat sich seit der Verifikation geändert.");

      var hashBytes = await SHA256.HashDataAsync(stream, ct).ConfigureAwait(false);
      var actual = Convert.ToHexString(hashBytes).ToLowerInvariant();

      if (!string.Equals(actual, Sha256, StringComparison.Ordinal))
        throw new UpdateSecurityException("Setup-Start blockiert: Die Datei wurde nach der Verifikation verändert (SHA256 weicht ab).");

      return new SetupLaunchLock(stream);
    }
    catch
    {
      stream.Dispose();
      throw;
    }
  }

  public void Dispose() => _workspace.Dispose();
}

/// <summary>
/// Hält die verifizierte Setup-Datei offen (Schreib-/Löschzugriffe gesperrt),
/// bis der Prozessstart abgeschlossen ist.
/// </summary>
public sealed class SetupLaunchLock : IDisposable
{
  private readonly FileStream _stream;

  internal SetupLaunchLock(FileStream stream)
  {
    _stream = stream;
  }

  public void Dispose() => _stream.Dispose();
}

/// <summary>
/// Startet ausschließlich ein erfolgreich verifiziertes Setup. Die eigentliche
/// Prozess-API wird injiziert, damit der Ablauf testbar bleibt.
/// </summary>
public static class UpdateInstaller
{
  /// <summary>
  /// Prüft das Paket erneut (Existenz, Name, Größe, Hash), hält die Datei während
  /// des Starts gesperrt und ruft dann <paramref name="startProcess"/> mit dem
  /// absoluten Pfad auf. Liefert nur bei tatsächlich gestartetem Setup true;
  /// jeder Fehler wirft, sodass der Aufrufer die App am Leben lässt.
  /// </summary>
  public static async Task<bool> LaunchVerifiedSetupAsync(
    VerifiedSetupPackage package,
    Func<string, bool> startProcess,
    CancellationToken ct = default)
  {
    ArgumentNullException.ThrowIfNull(package);
    ArgumentNullException.ThrowIfNull(startProcess);

    using var launchLock = await package.AcquireLaunchLockAsync(ct).ConfigureAwait(false);

    bool started;
    try
    {
      started = startProcess(package.FilePath);
    }
    catch (Exception ex)
    {
      throw new UpdateDownloadException($"Setup konnte nicht gestartet werden: {ex.Message}", ex);
    }

    if (!started)
      throw new UpdateDownloadException("Setup wurde nicht gestartet (kein Prozess erzeugt).");

    return true;
  }
}
