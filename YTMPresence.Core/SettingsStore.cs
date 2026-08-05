using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace YTMPresence.Core;

public static class SettingsStore
{
  private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
  private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

  public static string GetDefaultSettingsPath()
  {
    var dir = GetConfigDir();
    Directory.CreateDirectory(dir);
    return Path.Combine(dir, "settings.json");
  }

  public static AppSettings LoadOrCreateDefault(string settingsPath)
  {
    if (!File.Exists(settingsPath))
    {
      var defaults = new AppSettings();
      Normalize(defaults);
      Save(settingsPath, defaults);
      return defaults;
    }

    try
    {
      var json = File.ReadAllText(settingsPath);
      var loaded = JsonSerializer.Deserialize<AppSettings>(json)
          ?? throw new JsonException("Settings document must contain a JSON object.");

      Normalize(loaded);
      Save(settingsPath, loaded);
      return loaded;
    }
    catch (Exception ex) when (ex is JsonException or NotSupportedException)
    {
      return RecoverCorruptedSettings(settingsPath);
    }
  }

  public static void Save(string settingsPath, AppSettings settings)
  {
    Normalize(settings);

    var fullPath = Path.GetFullPath(settingsPath);
    var directory = Path.GetDirectoryName(fullPath)
        ?? throw new InvalidOperationException("Settings path has no parent directory.");

    Directory.CreateDirectory(directory);

    var json = JsonSerializer.Serialize(settings, JsonOptions);
    var temporaryPath = Path.Combine(
        directory,
        $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");

    try
    {
      using (var stream = new FileStream(
          temporaryPath,
          FileMode.CreateNew,
          FileAccess.Write,
          FileShare.None,
          bufferSize: 16 * 1024,
          FileOptions.WriteThrough))
      {
        using (var writer = new StreamWriter(stream, Utf8WithoutBom, bufferSize: 16 * 1024, leaveOpen: true))
        {
          writer.Write(json);
        }

        stream.Flush(flushToDisk: true);
      }

      if (File.Exists(fullPath))
      {
        File.Replace(
            temporaryPath,
            fullPath,
            destinationBackupFileName: null,
            ignoreMetadataErrors: true);
      }
      else
      {
        File.Move(temporaryPath, fullPath);
      }
    }
    finally
    {
      try
      {
        if (File.Exists(temporaryPath))
          File.Delete(temporaryPath);
      }
      catch
      {
        // Eine fehlgeschlagene Temp-Bereinigung darf den ursprünglichen
        // Speicherfehler nicht verdecken.
      }
    }
  }

  private static AppSettings RecoverCorruptedSettings(string settingsPath)
  {
    var fullPath = Path.GetFullPath(settingsPath);
    var directory = Path.GetDirectoryName(fullPath)
        ?? throw new InvalidOperationException("Settings path has no parent directory.");
    var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fullPath);
    var extension = Path.GetExtension(fullPath);
    var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmssfff");
    var backupPath = Path.Combine(
        directory,
        $"{fileNameWithoutExtension}.corrupt-{timestamp}{extension}");

    for (var suffix = 1; File.Exists(backupPath); suffix++)
    {
      backupPath = Path.Combine(
          directory,
          $"{fileNameWithoutExtension}.corrupt-{timestamp}-{suffix}{extension}");
    }

    File.Move(fullPath, backupPath);

    try
    {
      var defaults = new AppSettings();
      Normalize(defaults);
      Save(fullPath, defaults);
      return defaults;
    }
    catch
    {
      // Recovery darf die beschädigte Originaldatei nicht verlieren, falls
      // das Schreiben der neuen Defaults selbst fehlschlägt.
      if (!File.Exists(fullPath) && File.Exists(backupPath))
        File.Move(backupPath, fullPath);

      throw;
    }
  }

  private static void Normalize(AppSettings s)
  {
    s.DiscordClientId = (s.DiscordClientId ?? "").Trim();

    s.ListenHost = string.IsNullOrWhiteSpace(s.ListenHost) ? "127.0.0.1" : s.ListenHost.Trim();
    s.ListenPort = Clamp(s.ListenPort, 1, 65535);

    s.WebSocketPath = string.IsNullOrWhiteSpace(s.WebSocketPath) ? "/ws" : EnsureSlash(s.WebSocketPath.Trim());

    s.MinDiscordUpdateSeconds = Clamp(s.MinDiscordUpdateSeconds, 5, 300);
    s.IdleClearSeconds = Clamp(s.IdleClearSeconds, 10, 3600);
    s.PausedClearSeconds = Clamp(s.PausedClearSeconds, 10, 3600);

    s.Assets ??= new DiscordAssetKeys();
    s.Assets.LargeImageKey = (s.Assets.LargeImageKey ?? "").Trim().ToLowerInvariant();
    s.Assets.PlaySmallImageKey = (s.Assets.PlaySmallImageKey ?? "").Trim().ToLowerInvariant();
    s.Assets.PauseSmallImageKey = (s.Assets.PauseSmallImageKey ?? "").Trim().ToLowerInvariant();
    s.Assets.LargeImageText = (s.Assets.LargeImageText ?? "YouTube Music").Trim();

    s.PlayerWindow ??= new PlayerWindowSettings();
    s.PlayerWindow.Width = ClampDouble(s.PlayerWindow.Width, 460, 1200, 520);
    s.PlayerWindow.Height = ClampDouble(s.PlayerWindow.Height, 200, 900, 250);

    if (s.PlayerWindow.Left is double left && !double.IsFinite(left))
      s.PlayerWindow.Left = null;

    if (s.PlayerWindow.Top is double top && !double.IsFinite(top))
      s.PlayerWindow.Top = null;

    // Token automatisch generieren, wenn leer
    s.SecurityToken = (s.SecurityToken ?? "").Trim();
    if (string.IsNullOrWhiteSpace(s.SecurityToken))
      s.SecurityToken = SecurityTokenHelper.GenerateSecureToken();
  }

  private static int Clamp(int v, int min, int max) => v < min ? min : (v > max ? max : v);
  private static double ClampDouble(double v, double min, double max, double fallback)
  {
    if (!double.IsFinite(v)) return fallback;
    return v < min ? min : (v > max ? max : v);
  }

  private static string EnsureSlash(string path) => path.StartsWith('/') ? path : "/" + path;

  private static string GetConfigDir()
  {
    if (OperatingSystem.IsWindows())
    {
      var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
      return Path.Combine(appData, "YTMPresence");
    }

    if (OperatingSystem.IsMacOS())
    {
      var home = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
      return Path.Combine(home, "Library", "Application Support", "YTMPresence");
    }

    var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
    if (!string.IsNullOrWhiteSpace(xdg))
      return Path.Combine(xdg, "YTMPresence");

    var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    return Path.Combine(userHome, ".config", "YTMPresence");
  }
}
