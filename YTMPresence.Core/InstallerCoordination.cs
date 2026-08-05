using System.Text;
using System.Security.Cryptography;

namespace YTMPresence.Core;

/// <summary>
/// Benennt die Kernel-Objekte, über die sich Installer und laufende App
/// koordinieren. Beide Namen sind an den Installationspfad gebunden, damit
/// der Installer ausschließlich die Instanz aus genau diesem Zielpfad
/// anspricht — nie eine portable Kopie oder einen fremden Prozess mit
/// gleichem Dateinamen.
///
/// Die Namenslogik muss exakt mit <c>MakeInstanceKey</c> in
/// installer/YTMPresence.iss übereinstimmen (ASCII-Lowercase, SHA256 über die
/// UTF-16LE-Codeeinheiten des normalisierten Pfads).
/// </summary>
public static class InstallerCoordination
{
  // Local\ = Namespace der aktuellen Windows-Sitzung: Instanzen anderer
  // Benutzer bleiben unberührt; der Installer läuft in derselben Sitzung.
  public const string RunningMutexPrefix = @"Local\YTMPresence.Running.";
  public const string ShutdownEventPrefix = @"Local\YTMPresence.Shutdown.";

  /// <summary>Mutex-Name, der signalisiert, dass eine Instanz aus diesem Verzeichnis läuft.</summary>
  public static string GetRunningMutexName(string applicationDirectory)
    => RunningMutexPrefix + GetInstanceKey(applicationDirectory);

  /// <summary>Event-Name, über den der Installer diese Instanz um Beendigung bittet.</summary>
  public static string GetShutdownEventName(string applicationDirectory)
    => ShutdownEventPrefix + GetInstanceKey(applicationDirectory);

  /// <summary>
  /// Bildet ein Verzeichnis auf einen kollisionsresistenten, kompakten
  /// kernelobjekt-tauglichen Schlüssel ab. Die explizite UTF-16LE-Codierung
  /// entspricht Innos GetSHA256OfUnicodeString.
  /// </summary>
  public static string GetInstanceKey(string applicationDirectory)
  {
    if (string.IsNullOrWhiteSpace(applicationDirectory))
      throw new ArgumentException("Application directory must not be empty.", nameof(applicationDirectory));

    var full = Path.GetFullPath(applicationDirectory).TrimEnd('\\', '/');
    var normalized = new StringBuilder(full.Length);

    foreach (var c in full)
    {
      if (c is >= 'a' and <= 'z' or >= '0' and <= '9')
        normalized.Append(c);
      else if (c is >= 'A' and <= 'Z')
        normalized.Append((char)(c + 32));
      else
        normalized.Append(c);
    }

    var bytes = Encoding.Unicode.GetBytes(normalized.ToString());
    return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
  }
}
